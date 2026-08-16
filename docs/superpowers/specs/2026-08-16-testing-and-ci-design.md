# Testy automatyczne i CI dla SafeCare

Data: 2026-08-16
Status: zatwierdzony do implementacji

## Cel

SafeCare nie ma dziś ani projektu testowego, ani pipeline'u CI. Weryfikacja polega na
zbudowaniu aplikacji i ręcznym klikaniu po niej przy lokalnym PostgreSQL. Ten dokument
opisuje projekt zestawu testów pokrywającego logikę domenową oraz workflow GitHub Actions,
który uruchamia go na każdy push i pull request.

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
| Liczba projektów | jeden (`SafeCare.Tests`) | Zgodne z minimalizmem repo; podział przez `Trait`, nie przez projekt |
| Testy komponentów | poza zakresem | bUnit + MudBlazor jest kruchy i kosztowny w utrzymaniu |

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
    Mappings/                  IncidentReportMappingTests, AdminUserMappingTests, ...
    Email/                     IncidentEmailTemplateTests
    Enums/                     EnumDisplayTests
    SourceEncodingTests.cs
  Integration/
    Infrastructure/            PostgresFixture, IntegrationTestBase, TestDbContextFactory, FakeEmailQueue
    IncidentReportServiceTests/  Create, Query, Sorting, Details, Mutations
    UserManagementServiceTests.cs
    SeederTests.cs
```

Testy integracyjne noszą `[Trait("Category", "Integration")]`, więc
`dotnet test --filter Category!=Integration` przechodzi bez Dockera.

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
Analogicznie pozostałe mapowania.

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

## Pipeline CI

Plik `.github/workflows/ci.yml`, wyzwalany na `push` i `pull_request` do `main`, jeden job na
`ubuntu-latest` (runner ma Dockera, więc Testcontainers działa bez dodatkowej konfiguracji).

Kroki: checkout → `setup-dotnet` (10.0.x) → `restore` → `build -c Release --no-restore` →
`test -c Release --no-build` → skan podatności → weryfikacja migracji → kontrola formatowania.

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
- Testy end-to-end w przeglądarce.
- Testy wysyłki e-mail przez prawdziwy SMTP (`MailKitEmailService`, `GraphEmailService`) —
  wymagałyby atrapy serwera pocztowego; pokryty jest szablon i kolejkowanie.
- Bramka pokrycia kodu (`coverlet` z progiem) — możliwa do dodania później.

## Dokumentacja do aktualizacji

`CLAUDE.md`, `AGENTS.md` i `README.md` stwierdzają dziś, że projekt nie ma testów ani CI.
Wszystkie trzy wymagają korekty wraz z opisem sposobu uruchamiania testów i wymagania Dockera
dla warstwy integracyjnej.
