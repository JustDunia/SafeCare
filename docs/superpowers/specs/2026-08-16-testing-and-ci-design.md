# Testy automatyczne i CI dla SafeCare

Data: 2026-08-16
Status: zatwierdzony do implementacji

## Cel

SafeCare nie ma dziś ani projektu testowego, ani pipeline'u CI. Weryfikacja polega na
zbudowaniu aplikacji i ręcznym klikaniu po niej przy lokalnym PostgreSQL. Ten dokument
opisuje trzy warstwy testów — jednostkową, integracyjną na prawdziwym PostgreSQL i E2E w
przeglądarce — oraz workflow GitHub Actions, który uruchamia je na każdy push i pull request.

Zakres celowo obejmuje te miejsca, w których projekt już miał defekty — sortowanie i
filtrowanie siatki, kategoria „Inne", kodowanie plików źródłowych. Testy mają być strażnikiem
konkretnych regresji, nie ozdobą raportu pokrycia.

## Decyzje

| Decyzja | Wybór | Uzasadnienie |
|---|---|---|
| Framework | xUnit | Standard w ekosystemie .NET, natywne wsparcie `dotnet test` |
| Baza w testach | Testcontainers (PostgreSQL) | Ta sama ścieżka lokalnie i w CI, pełna izolacja |
| Asercje | wbudowany `Assert` xUnit | Bez dodatkowej zależności; FluentAssertions v8 ma licencję komercyjną |
| Mocki | ręczne atrapy | `IEmailQueue` ma dwie metody, `ILogger<T>` pokrywa `NullLogger<T>` |
| Liczba projektów | `SafeCare.Tests` + `SafeCare.E2ETests` | E2E wymaga przeglądarek i hostowania aplikacji — osobny projekt trzyma szybki zestaw szybkim |
| Testy komponentów | poza zakresem | bUnit + MudBlazor jest kruchy i kosztowny w utrzymaniu; ścieżki UI pokrywa E2E |
| Testy E2E | Playwright dla .NET | C# w tej samej solucji, dostęp do encji przy weryfikacji stanu po akcji w UI |
| Hosting w E2E | proces zewnętrzny na Kestrelu | `WebApplicationFactory` serwuje przez `TestServer` w pamięci, niedostępny dla przeglądarki |
| Poczta w E2E | MailPit w kontenerze | Jedyne pokrycie `EmailBackgroundService` i realnej wysyłki SMTP |

### Dlaczego prawdziwy PostgreSQL, a nie provider InMemory

`IncidentReportService.GetReports` filtruje przez `EF.Functions.ILike`, co jest rozszerzeniem
Npgsql. Provider InMemory i SQLite rzucą przy próbie translacji tego wyrażenia. Ponieważ to
właśnie filtrowanie i sortowanie są historycznym źródłem defektów, testowanie ich na atrapie
providera byłoby testowaniem czegoś innego niż kod produkcyjny. Wniosek: warstwa integracyjna
musi jechać na prawdziwym silniku.

## Architektura projektu testowego

```
SafeCare.Tests/
  SafeCare.Tests.csproj        ProjectReference → SafeCare
  Unit/
    Validators/                IncidentRegistrationFormValidatorTests, AdminUserCreateValidatorTests
    Services/                  BotDetectionServiceTests, RateLimitServiceTests
    Mappings/                  IncidentReportMappingTests
    Email/                     IncidentEmailTemplateTests
    Enums/                     EnumDisplayTests
    SourceEncodingTests.cs
  Integration/
    Infrastructure/            PostgresFixture, IntegrationTestBase, TestDbContextFactory, FakeEmailQueue
    IncidentReportServiceTests/  Create, Query, Sorting, Details, Mutations
    UserManagementServiceTests.cs
    SeederTests.cs

SafeCare.E2ETests/
  SafeCare.E2ETests.csproj     ProjectReference → SafeCare (dla weryfikacji stanu w bazie)
  Infrastructure/              AppFixture, MailPitClient, PageObjects
  PublicFormTests.cs
  AuthenticationTests.cs
  AuthorizationTests.cs
  DashboardTests.cs
  IncidentDetailsTests.cs
```

Oba projekty zostają dopisane do `SafeCare.slnx`, który zawiera dziś wyłącznie projekt aplikacji.

Testy integracyjne noszą `[Trait("Category", "Integration")]`, więc
`dotnet test SafeCare.Tests -- --filter-not-trait "Category=Integration"` przechodzi bez
Dockera. Projekt E2E jest osobny, żeby `dotnet test SafeCare.Tests` pozostał szybki i nie
wymagał przeglądarek.

### Toolchain — ustalenia zweryfikowane doświadczalnie

xUnit v3 na .NET 10 SDK działa inaczej niż v2 i inaczej niż opisuje większość materiałów.
Poniższe punkty zostały sprawdzone na działającym projekcie próbnym, nie założone:

- **VSTest jest martwy.** Na SDK 10 i nowszym `Microsoft.Testing.Platform` odmawia pracy przez
  target VSTest. Projekt testowy **nie** referencuje `Microsoft.NET.Test.Sdk` ani
  `xunit.runner.visualstudio` — obecność tych pakietów kieruje `dotnet test` w martwą ścieżkę.
- **Wymagany opt-in w `global.json`** w katalogu głównym repozytorium:
  `{ "test": { "runner": "Microsoft.Testing.Platform" } }`. Bez tego `dotnet test` kończy się
  błędem niezależnie od zawartości projektu. Wariant z plikiem `dotnet.config` nie jest tu
  honorowany. Sekcja `sdk` celowo pominięta — opt-in działa zarówno na SDK 10, jak i 11.
- **Projekt testowy jest aplikacją:** `<OutputType>Exe</OutputType>`.
- **`IAsyncLifetime` w v3 zwraca `ValueTask`**, nie `Task`.
- **Filtrowanie po cechach** ma składnię `-- --filter-trait "Klucz=Wartość"` oraz
  `--filter-not-trait`; wyrażenia VSTest w rodzaju `--filter "Category!=Integration"` nie działają.

### Infrastruktura integracyjna

`PostgresFixture` implementuje `IAsyncLifetime` i jest współdzielony przez wszystkie klasy
integracyjne za pomocą `[CollectionDefinition]`. Odpowiada za:

1. start jednego kontenera `postgres` na cały przebieg testów,
2. jednorazowe zaaplikowanie prawdziwych migracji EF (`Database.MigrateAsync()`),
3. udostępnienie connection stringa.

Izolacja danych między testami: `IntegrationTestBase` czyści tabele domenowe (`TRUNCATE ...
RESTART IDENTITY CASCADE`) przed każdym testem. Migracje nie są odtwarzane — to najdroższy
krok, a czyszczenie tabel daje ten sam determinizm.

Testy otrzymują `IDbContextFactory<AppDbContext>` — dokładnie ten kontrakt, którego używa
produkcja. `TestDbContextFactory` to cienka implementacja zwracająca
`new AppDbContext(options)`.

Dla `UserManagementService` budowany jest realny `UserManager<User>` przez `ServiceCollection`
z `AddIdentityCore<User>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>()`,
z konfiguracją wziętą z produkcyjnego `IdentityConfig`. Dzięki temu testy weryfikują faktyczną
politykę haseł. Rejestracja `AppDbContext` jako scoped wewnątrz kontenera testowego nie łamie
zasady „nigdy nie wstrzykuj kontekstu" — ta dotyczy obwodów Blazor Server, nie testów.

`FakeEmailQueue` gromadzi zakolejkowane wiadomości w liście i pozwala włączyć tryb rzucający
wyjątkiem, potrzebny do testu opisanego niżej.

## Pokrycie — warstwa jednostkowa

**`IncidentRegistrationFormValidator`** — największy pojedynczy obszar:

- imiona z polskimi znakami diakrytycznymi („Paweł", „Łukasz", „Żaneta") przechodzą — regresja
  na awarię kodowania, przez którą regex odrzucał te nazwiska,
- formy złożone: „Anna-Maria", „O'Brien", nazwiska dwuczłonowe ze spacją,
- cyfry i znaki specjalne odrzucane; granice długości 2–50,
- puste pola zgłaszającego i pacjenta są poprawne (anonimowość jest zamierzona),
- telefon: `+48` z separatorem i bez, odrzucenie numeru zaczynającego się od zera,
- `PatientDob`: data przyszła i starsza niż 120 lat odrzucone, granice włącznie,
- gałąź `IsDatePeriod == true`: `DateFrom`/`DateTo` wymagane, nie w przyszłości,
  `DateTo >= DateFrom`,
- gałąź `IsDatePeriod == false`: `Date` i `Time` wymagane, zdarzenie z dzisiejszą datą i
  godziną w przyszłości odrzucone (przypadek, który sama walidacja daty by przepuściła),
- oddział wymagany; wymagany co najmniej jeden rodzaj zdarzenia **lub** opis własny —
  oba warianty osobno,
- opis wymagany, limit 5000 znaków.

**`AdminUserCreateValidator`** — pola wymagane, limity długości, zgodność `ConfirmPassword`
z `Password`, rola wyłącznie `Admin` lub `User`.

**`BotDetectionService`** — zgłoszenie szybsze niż 5 s odrzucone; każde z trzech pól pułapki
(`Email2`, `Website`, `Address`) odrzuca osobno; czysty formularz po upływie czasu przechodzi.

**`RateLimitService`** — pięć zgłoszeń przechodzi, szóste nie; `GetTimeUntilReset` zwraca
`null` przed osiągnięciem limitu i wartość po nim; różne `clientId` oraz różne `action`
prowadzą niezależne liczniki.

**Mapowania** — `IncidentRegistrationFormVm.ToDto()` przenosi wszystkie pola; gałąź okresu
zeruje `Date`, a gałąź pojedynczej daty składa `Date` z `Time` i zeruje `DateFrom`/`DateTo`;
niepoprawny format czasu rzuca `ArgumentException`; brak oddziału lub opisu rzuca
`ArgumentNullException`; `PatientGender == null` mapuje się na `Gender.NotProvided`.

Pozostałe mapowania (`DepartmentMapping`, `IncidentDefinitionMapping`, `AdminUserMapping`)
zostają bez pokrycia świadomie: przepisują pola jeden do jednego, więc test powtarzałby
inicjalizator obiektu i wymagał aktualizacji przy każdym nowym polu, nie wykrywając niczego.

**`IncidentReport` — walidacja w konstruktorze.** Encja sama pilnuje reguły „dokładna data albo
kompletny zakres, nigdy w przyszłości" i rzuca `DomainException`. To czysta logika domenowa bez
I/O, więc testowana jest jednostkowo: brak obu wariantów daty, zakres odwrócony, zakres w
przyszłości, konkretna data w przyszłości oraz normalizacja `DateFrom`/`DateTo` do samej daty.
Reguła ta dubluje się częściowo z walidatorem formularza, ale broni też ścieżek pomijających
UI — dlatego ma własne pokrycie.

**`IncidentEmailTemplate.Build`** — odbiorcy trafiają do BCC, temat zawiera numer zgłoszenia,
treść zawiera oddział i opis, a wartości pochodzące od użytkownika są zakodowane HTML-owo
(ochrona przed wstrzyknięciem znaczników do wiadomości).

**Enumy** — przebieg po wszystkich wartościach `IncidentCategory`, `ReportStatus` i `Gender`:
`GetDisplayName()` zwraca etykietę różną od nazwy członka, a `GetColor()`/`GetIcon()` na
`ReportStatus` nie wpadają w gałąź domyślną. Test wyłapie enum rozszerzony bez uzupełnienia
odwzorowań.

**`SourceEncodingTests`** — skan wszystkich plików `.cs` i `.razor` w repozytorium pod kątem
poprawności sekwencji UTF-8. Adresuje udokumentowaną awarię, w której trzy pliki zapisane jako
Windows-1250 przeszły kompilację i rozsypały cały formularz publiczny.

## Pokrycie — warstwa integracyjna

**`IncidentReportService.CreateReport`** — zgłoszenie zapisuje się z powiązanym oddziałem i
definicjami zdarzeń; nieistniejący oddział oraz nieistniejące definicje dają
`EntityNotFoundException` z poprawnym zestawem brakujących identyfikatorów; e-mail kolejkowany
jest wyłącznie do użytkowników z `ReceiveEmailNotifications` i niepustym adresem; **rzucający
`IEmailQueue` nie przerywa zapisu zgłoszenia** — to udokumentowana decyzja projektowa
(„a broken mail server must not fail a patient's report") i test ma jej pilnować.

**`GetReports` — filtrowanie:**

- `FullName` i `PatientFullName` dopasowywane bez względu na wielkość liter, z polskimi znakami,
- `Gender`, `Department`, `Statuses`,
- kategorie: zgłoszenie mające wyłącznie `OtherIncidentDefinition` **jest** zwracane przy
  filtrze „Inne" i **nie jest** zwracane przy filtrze na kategorię słownikową — obie strony
  reguły, bo to ona cicho psuła filtr,
- escapowanie wildcardów: termin `100%` dopasowuje się literalnie, a nie jako wzorzec.

**`GetReports` — sortowanie:** każda kolumna rosnąco i malejąco, w tym dwa świadome wyjątki —
`PatientAge` sortuje odwrotnie do `PatientDob`, a `Department` po `Department.Name`, nie po
kluczu obcym. Do tego stronicowanie: druga strona zwraca właściwy wycinek, a `ItemTotalCount`
liczy wszystkie pasujące wiersze, nie tylko bieżącą stronę.

**`GetReportDetails`** — zwraca dane z rozwiązanym oddziałem i definicjami; niepuste
`OtherIncidentDefinition` dokłada dodatkowy wiersz z kategorią „Inne"; nieistniejące id daje
`DomainException`.

**`UpdateStatus` / `DeleteReport`** — ścieżka pozytywna oraz `DomainException` dla
nieistniejącego identyfikatora.

**`UserManagementService`** — komunikaty walidacji po polsku dla każdego pola; duplikat nazwy
i duplikat adresu e-mail; nieprawidłowa rola; wycofanie utworzonego konta, gdy przypisanie roli
zawiedzie; odmowa usunięcia własnego konta; odmowa usunięcia ostatniego administratora;
`GetUsersAsync` poprawnie rozstrzyga rolę i sortuje po nazwie.

**Seedery** — `DbSeeder.SeedAsync` ładuje dane słownikowe i jest idempotentny (drugie wywołanie
nie duplikuje); `IdentitySeeder` tworzy role i rzuca, gdy konto `admin` z migracji zniknęło.

## Pokrycie — warstwa E2E

### Uruchamianie aplikacji pod testem

`AppFixture` (jedna instancja na cały przebieg) wykonuje kolejno:

1. start kontenera PostgreSQL i kontenera MailPit,
2. start aplikacji jako **procesu zewnętrznego** (`dotnet run` na skompilowanym wyjściu),
   nasłuchującego na wolnym porcie pod `http://127.0.0.1:{port}`,
3. oczekiwanie na gotowość przez odpytywanie strony głównej z limitem czasu,
4. start Playwrighta i przeglądarki Chromium.

Konfiguracja wstrzykiwana jest zmiennymi środowiskowymi:
`ConnectionStrings__DefaultConnection`, `Email__Smtp__Host`, `Email__Smtp__Port`,
`ASPNETCORE_URLS` oraz `ASPNETCORE_ENVIRONMENT=Testing`.

Ostatnia zmienna pełni funkcję zabezpieczenia: środowisko `Testing` nie ładuje
`appsettings.Development.json`, więc test nie ma jak trafić w deweloperską bazę danych.
Zmienne środowiskowe i tak mają pierwszeństwo przed plikami JSON, ale ta ochrona jest
warstwowa celowo — pomyłka tutaj oznacza skasowanie cudzych danych roboczych.

Aplikacja startuje wyłącznie na adresie HTTP. `UseHttpsRedirection` nie mając skąd odczytać
portu HTTPS pomija przekierowanie i tylko loguje ostrzeżenie, więc testy nie potrzebują
certyfikatu deweloperskiego. Środowisko inne niż `Development` włącza natomiast
`UseExceptionHandler` — to zamierzone, bo E2E ma widzieć aplikację taką, jaka trafia do
użytkownika.

Migracje, dane słownikowe i konto `admin` powstają same przy starcie aplikacji, bo `Program.cs`
robi to przed obsłużeniem pierwszego żądania. Testy nie potrzebują własnego zasiewu.

Selektory oparte są o obiekty stron (Page Objects), a nie o klasy CSS MudBlazora, które zmieniają
się między wersjami biblioteki.

### Ograniczenie: minimalny czas wypełnienia formularza

`BotDetectionService` odrzuca zgłoszenia wysłane szybciej niż 5 sekund po załadowaniu
formularza. Każdy test E2E przechodzący przez wysyłkę musi ten próg przeczekać, co jest
nieusuwalnym kosztem — próg jest funkcją bezpieczeństwa, nie usterką. Testy odczekują jawnie
i z komentarzem, żeby nikt nie „zoptymalizował" tego później w ciemno.

### Scenariusze

**Formularz publiczny** — wypełnienie wszystkich sekcji i wysłanie zgłoszenia, potwierdzenie
komunikatem w UI, a następnie weryfikacja w bazie, że zapisany rekord ma właściwy oddział,
rodzaje zdarzeń i opis. Osobno: zgłoszenie anonimowe, czyli z pustymi danymi zgłaszającego i
pacjenta, przechodzi — anonimowość jest zamierzona i test ma to utrwalić. Dalej: komunikaty
walidacyjne po polsku dla pustego opisu i braku oddziału, gałąź okresu (data od–do) obok gałęzi
pojedynczej daty z godziną, oraz wysyłka szybsza niż 5 sekund, która **kończy się cicho** —
bez komunikatu dla użytkownika, zgodnie z projektem obrony przed botami.

**Powiadomienie e-mail** — po wysłaniu zgłoszenia test odpytuje API MailPit i sprawdza, że
wiadomość dotarła, ma numer zgłoszenia w temacie, a odbiorcy są w BCC, nie w polu Do. Pokrywa
`EmailBackgroundService`, dziś nietestowany nigdzie indziej.

**Logowanie** — poprawne dane prowadzą na `/dashboard`; błędne wracają na `/login` z
komunikatem z parametru `?error=`; wylogowanie odcina dostęp do stron chronionych.

**Autoryzacja** — użytkownik anonimowy wchodzący na `/dashboard` ląduje na `/login` z
`returnUrl`, a po zalogowaniu trafia **z powrotem na stronę, o którą prosił**. Ten test celuje
w udokumentowaną ostrą krawędź: `/signin` przyjmuje wyłącznie ścieżki zaczynające się od `/`,
a `ToBaseRelativePath` zwraca ścieżkę bez wiodącego ukośnika, więc pomyłka w
`RedirectUnauthorized` po cichu gubi każdy `returnUrl`. Drugi przypadek: zalogowany użytkownik
bez roli `Admin` wchodzący na `/admin/users` jest przekierowany na `/dashboard`, a nie na ekran
logowania.

**Dashboard** — filtrowanie i sortowanie zawężają siatkę, a stan filtrów wraca w adresie URL:
otwarcie tego adresu na nowo odtwarza ten sam widok. To sprawdza wymóg zakładkowalności
wprost. Do tego stronicowanie.

**Szczegóły zgłoszenia** — wejście z siatki w `/details/{id}` pokazuje dane zgodne z wierszem,
a zmiana statusu utrwala się po odświeżeniu strony.

## Pipeline CI

Plik `.github/workflows/ci.yml`, wyzwalany na `push` i `pull_request` do `main`, na
`ubuntu-latest` (runner ma Dockera, więc Testcontainers działa bez dodatkowej konfiguracji).
Dwa joby, uruchamiane równolegle:

**Job `build-test`** — checkout → `setup-dotnet` (10.0.x) → `restore` →
`build -c Release --no-restore` → `test SafeCare.Tests -c Release --no-build` → skan podatności
→ weryfikacja migracji → kontrola formatowania.

**Job `e2e`** — checkout → `setup-dotnet` → `build -c Release` → instalacja przeglądarek
Playwrighta (`playwright.ps1 install --with-deps chromium`) → `test SafeCare.E2ETests`. W razie
niepowodzenia publikuje ślady Playwrighta jako artefakt przebiegu — bez nich diagnoza padniętego
testu przeglądarkowego na cudzej maszynie jest zgadywanką.

Podział na dwa joby jest celowy: testy E2E są z natury wolniejsze i bardziej podatne na
chwiejność, a rozdzielone nie przesłaniają wyniku szybkiego zestawu.

**Skan podatności.** `dotnet list package --vulnerable --include-transitive` kończy się kodem 0
nawet gdy coś znajdzie, więc krok musi analizować wyjście i sam wymusić niepowodzenie. Projekt
ma już przypięty `Microsoft.Kiota.Abstractions` obchodzący GHSA-7j59-v9qr-6fq9 — przed
uczynieniem tego kroku twardym trzeba potwierdzić, że przy tym przypięciu skan jest czysty.

**Weryfikacja migracji.** `dotnet ef migrations has-pending-model-changes` wykrywa rozjechanie
modelu EF z ostatnią migracją. Nie potrzebuje żywej bazy, ale potrzebuje działającej fabryki
projektowej, a `AppDbContextFactory` czyta `appsettings.Development.json`, który jest w
`.gitignore`. Workflow wygeneruje ten plik z atrapą connection stringa tuż przed tym krokiem.

**Kontrola formatowania.** `dotnet format --verify-no-changes` zgodnie z `.editorconfig`.
Istniejący kod nigdy nie przechodził tej bramki, więc krok jest ryzykiem: jeżeli pierwsze
uruchomienie pokaże duży diff, nie następuje przeformatowanie całego drzewa. Decyzja o zawężeniu
bramki (np. do białych znaków) należy wtedy do właściciela repozytorium.

## Poza zakresem

- Testy komponentów Blazor (bUnit) — uzasadnienie w tabeli decyzji.
- E2E w przeglądarkach innych niż Chromium oraz testy responsywności.
- `GraphEmailService` — ścieżka Microsoft Graph wymagałaby atrapy OAuth2; pokryta jest ścieżka
  SMTP, która jest domyślną konfiguracją aplikacji.
- Bramka pokrycia kodu (`coverlet` z progiem) — możliwa do dodania później.

## Dokumentacja do aktualizacji

`CLAUDE.md`, `AGENTS.md` i `README.md` stwierdzają dziś, że projekt nie ma testów ani CI.
Wszystkie trzy wymagają korekty: sposób uruchamiania obu zestawów, wymaganie Dockera dla
warstwy integracyjnej i E2E oraz jednorazowa instalacja przeglądarek Playwrighta.
