# Testy automatyczne i CI — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dodać do SafeCare trzy warstwy testów (jednostkowe, integracyjne na prawdziwym PostgreSQL, E2E w przeglądarce) oraz pipeline GitHub Actions uruchamiający je na każdy push i pull request.

**Architecture:** Dwa nowe projekty w solucji. `SafeCare.Tests` zawiera testy jednostkowe i integracyjne; te drugie dzielą jeden kontener PostgreSQL uruchamiany przez Testcontainers i czyszczą tabele między testami. `SafeCare.E2ETests` startuje kontenery PostgreSQL i MailPit, uruchamia aplikację jako proces zewnętrzny na Kestrelu i steruje nią przez Playwright. CI ma dwa równoległe joby.

**Tech Stack:** .NET 10, xUnit v3 (Microsoft.Testing.Platform), Testcontainers 4.14, Microsoft.Playwright 1.62, PostgreSQL 17, MailPit, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-16-testing-and-ci-design.md`

## Global Constraints

Te reguły obowiązują w **każdym** zadaniu. Zostały zweryfikowane doświadczalnie na projekcie próbnym — nie są założeniami.

- **Target framework:** `net10.0` dla obu projektów testowych.
- **xUnit v3 wymaga `<OutputType>Exe</OutputType>`** w projekcie testowym. Bez tego projekt się nie uruchomi.
- **NIE dodawaj `Microsoft.NET.Test.Sdk` ani `xunit.runner.visualstudio`.** Na .NET 10 SDK kierują `dotnet test` w ścieżkę VSTest, która kończy się twardym błędem: „Testing with VSTest target is no longer supported".
- **`global.json` w katalogu głównym repozytorium jest obowiązkowy** i musi zawierać `{ "test": { "runner": "Microsoft.Testing.Platform" } }`. Bez tego `dotnet test` nie działa. Nie dodawaj sekcji `sdk`.
- **`IAsyncLifetime` w xUnit v3 zwraca `ValueTask`**, nie `Task`. Sygnatury: `public async ValueTask InitializeAsync()` i `public async ValueTask DisposeAsync()`.
- **Filtrowanie po cechach:** `dotnet test <proj> -- --filter-trait "Category=Integration"` oraz `--filter-not-trait`. Składnia VSTest (`--filter "Category!=Integration"`) nie działa.
- **Asercje:** wyłącznie wbudowany `Assert` xUnit. Nie dodawaj FluentAssertions ani Shouldly.
- **Mocki:** ręczne atrapy. Nie dodawaj Moq ani NSubstitute. `ILogger<T>` pokrywa `NullLogger<T>` z `Microsoft.Extensions.Logging.Abstractions`.
- **Kodowanie plików:** wszystkie nowe pliki źródłowe zapisuj jako **UTF-8 z BOM** (`.editorconfig` wymusza `charset = utf-8-bom`). Testy zawierają polskie znaki w danych — zapis w Windows-1250 zepsuje je nieodwracalnie.
- **Język:** nazwy testów i komentarze po angielsku, dane testowe i oczekiwane komunikaty po polsku (bo takie są komunikaty aplikacji).
- **Wszystkie testy integracyjne** noszą `[Trait("Category", "Integration")]`.
- **Identyfikator zasianego administratora:** `62228aa3-8032-4d31-8b99-719629d26bb7` (z `HasData` w `User.cs`). Konto `admin`, hasło `Admin123!`.
- **Nazwa tabeli łączącej** M:N: `IncidentDefinitionIncidentReport`.
- **Commity:** każde zadanie kończy się commitem. Wiadomości po angielsku.

## Struktura plików

| Plik | Odpowiedzialność |
|---|---|
| `global.json` | Opt-in runnera testowego |
| `SafeCare.slnx` | Rejestracja obu projektów testowych |
| `SafeCare.Tests/SafeCare.Tests.csproj` | Projekt szybkiego zestawu |
| `SafeCare.Tests/Unit/**` | Testy bez I/O |
| `SafeCare.Tests/Integration/Infrastructure/**` | Kontener, fabryka kontekstu, atrapa kolejki, baza klas |
| `SafeCare.Tests/Integration/**` | Testy serwisów na prawdziwej bazie |
| `SafeCare.E2ETests/SafeCare.E2ETests.csproj` | Projekt testów przeglądarkowych |
| `SafeCare.E2ETests/Infrastructure/**` | Uruchamianie aplikacji, klient MailPit, obiekty stron |
| `SafeCare.E2ETests/*Tests.cs` | Scenariusze E2E |
| `.github/workflows/ci.yml` | Pipeline |

---

### Task 1: Szkielet projektu testowego + testy BotDetectionService

Scaffolding jest złożony w to zadanie, bo pierwszy prawdziwy test jest dowodem, że toolchain działa.

**Files:**
- Create: `global.json`
- Create: `SafeCare.Tests/SafeCare.Tests.csproj`
- Create: `SafeCare.Tests/Unit/Services/BotDetectionServiceTests.cs`
- Modify: `SafeCare.slnx`

**Interfaces:**
- Consumes: nic (pierwsze zadanie)
- Produces: projekt `SafeCare.Tests` z referencją do `SafeCare`, gotowy na kolejne testy

- [ ] **Step 1: Utwórz `global.json` w katalogu głównym**

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

- [ ] **Step 2: Utwórz `SafeCare.Tests/SafeCare.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="4.0.0" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.14.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SafeCare\SafeCare.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Zarejestruj projekt w `SafeCare.slnx`**

```xml
<Solution>
  <Project Path="SafeCare/SafeCare.csproj" />
  <Project Path="SafeCare.Tests/SafeCare.Tests.csproj" />
</Solution>
```

- [ ] **Step 4: Napisz testy `BotDetectionService`**

Plik `SafeCare.Tests/Unit/Services/BotDetectionServiceTests.cs`. `HoneypotData` to typ z `SafeCare.Services` — sprawdź jego dokładny kształt w `IBotDetectionService.cs` i dostosuj inicjalizację, jeśli nazwy właściwości różnią się od użytych niżej (`FormLoadedAt`, `Email2`, `Website`, `Address`).

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using SafeCare.Services;

namespace SafeCare.Tests.Unit.Services;

public class BotDetectionServiceTests
{
    private static BotDetectionService CreateSut() => new(NullLogger<BotDetectionService>.Instance);

    /// <summary>A form loaded long enough ago to have been filled in by a human.</summary>
    private static HoneypotData HumanLikeData() => new()
    {
        FormLoadedAt = DateTime.UtcNow.AddSeconds(-30),
        Email2 = null,
        Website = null,
        Address = null
    };

    [Fact]
    public void AcceptsSubmissionFilledSlowlyWithEmptyHoneypots()
    {
        var result = CreateSut().ValidateSubmission(HumanLikeData());

        Assert.True(result);
    }

    [Fact]
    public void RejectsSubmissionFasterThanMinimumFillTime()
    {
        var data = HumanLikeData();
        data.FormLoadedAt = DateTime.UtcNow.AddSeconds(-1);

        Assert.False(CreateSut().ValidateSubmission(data));
    }

    [Fact]
    public void AcceptsSubmissionExactlyAtTheFillTimeBoundary()
    {
        var data = HumanLikeData();
        data.FormLoadedAt = DateTime.UtcNow.AddSeconds(-6);

        Assert.True(CreateSut().ValidateSubmission(data));
    }

    [Fact]
    public void RejectsWhenEmail2HoneypotIsFilled()
    {
        var data = HumanLikeData();
        data.Email2 = "bot@example.com";

        Assert.False(CreateSut().ValidateSubmission(data));
    }

    [Fact]
    public void RejectsWhenWebsiteHoneypotIsFilled()
    {
        var data = HumanLikeData();
        data.Website = "http://spam.example";

        Assert.False(CreateSut().ValidateSubmission(data));
    }

    [Fact]
    public void RejectsWhenAddressHoneypotIsFilled()
    {
        var data = HumanLikeData();
        data.Address = "ul. Spamowa 1";

        Assert.False(CreateSut().ValidateSubmission(data));
    }
}
```

- [ ] **Step 5: Uruchom testy**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

Oczekiwane: 6 testów, wszystkie przechodzą. Jeśli pojawi się błąd „Testing with VSTest target is no longer supported" — brakuje `global.json` albo nie jest w katalogu głównym repozytorium.

- [ ] **Step 6: Commit**

```bash
git add global.json SafeCare.slnx SafeCare.Tests
git commit -m "Add test project with bot detection coverage"
```

---

### Task 2: Testy RateLimitService

**Files:**
- Create: `SafeCare.Tests/Unit/Services/RateLimitServiceTests.cs`

**Interfaces:**
- Consumes: projekt `SafeCare.Tests` z Task 1
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

Limit to 5 zgłoszeń na minutę, klucz to para `clientId` + `action`.

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using SafeCare.Services;

namespace SafeCare.Tests.Unit.Services;

public class RateLimitServiceTests
{
    private const string Action = "submit-report";

    private static RateLimitService CreateSut() => new(NullLogger<RateLimitService>.Instance);

    [Fact]
    public void AllowsUpToTheConfiguredLimit()
    {
        using var sut = CreateSut();
        var client = Guid.NewGuid().ToString();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            Assert.True(sut.IsAllowed(client, Action), $"Attempt {attempt} should be allowed");
        }
    }

    [Fact]
    public void BlocksTheAttemptAfterTheLimit()
    {
        using var sut = CreateSut();
        var client = Guid.NewGuid().ToString();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            sut.IsAllowed(client, Action);
        }

        Assert.False(sut.IsAllowed(client, Action));
    }

    [Fact]
    public void TracksClientsIndependently()
    {
        using var sut = CreateSut();
        var noisyClient = Guid.NewGuid().ToString();
        var quietClient = Guid.NewGuid().ToString();

        for (var attempt = 1; attempt <= 6; attempt++)
        {
            sut.IsAllowed(noisyClient, Action);
        }

        Assert.True(sut.IsAllowed(quietClient, Action));
    }

    [Fact]
    public void TracksActionsIndependently()
    {
        using var sut = CreateSut();
        var client = Guid.NewGuid().ToString();

        for (var attempt = 1; attempt <= 6; attempt++)
        {
            sut.IsAllowed(client, "submit-report");
        }

        Assert.True(sut.IsAllowed(client, "some-other-action"));
    }

    [Fact]
    public void ReportsNoResetDelayBeforeTheLimitIsReached()
    {
        using var sut = CreateSut();
        var client = Guid.NewGuid().ToString();

        sut.IsAllowed(client, Action);

        Assert.Null(sut.GetTimeUntilReset(client, Action));
    }

    [Fact]
    public void ReportsRemainingWindowOnceTheLimitIsReached()
    {
        using var sut = CreateSut();
        var client = Guid.NewGuid().ToString();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            sut.IsAllowed(client, Action);
        }

        var remaining = sut.GetTimeUntilReset(client, Action);

        Assert.NotNull(remaining);
        Assert.InRange(remaining!.Value, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void ReportsNoResetDelayForAnUnknownClient()
    {
        using var sut = CreateSut();

        Assert.Null(sut.GetTimeUntilReset(Guid.NewGuid().ToString(), Action));
    }
}
```

- [ ] **Step 2: Uruchom testy**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

Oczekiwane: 13 testów przechodzi.

- [ ] **Step 3: Commit**

```bash
git add SafeCare.Tests/Unit/Services/RateLimitServiceTests.cs
git commit -m "Add rate limit service coverage"
```

---

### Task 3: Testy walidatora formularza publicznego

Największe pojedyncze zadanie. `IncidentRegistrationFormValidator` broni całego publicznego wejścia do systemu.

**Files:**
- Create: `SafeCare.Tests/Unit/Validators/IncidentRegistrationFormValidatorTests.cs`

**Interfaces:**
- Consumes: projekt z Task 1
- Produces: `ValidFormBuilder` — statyczna metoda pomocnicza budująca poprawny `IncidentRegistrationFormVm`; kolejne zadania jej nie używają (mają własne dane)

- [ ] **Step 1: Napisz testy**

Kluczowa zasada: pola zgłaszającego i pacjenta są **opcjonalne** — walidacja włącza się dopiero po wypełnieniu. Testy muszą to utrwalać, bo anonimowość jest zamierzona.

```csharp
using FluentValidation.Results;
using SafeCare.Dtos;
using SafeCare.Enums;
using SafeCare.Validators;
using SafeCare.ViewModels;

namespace SafeCare.Tests.Unit.Validators;

public class IncidentRegistrationFormValidatorTests
{
    private readonly IncidentRegistrationFormValidator _sut = new();

    /// <summary>
    /// A form carrying only what the rules actually require: when it happened, where,
    /// what kind of event it was, and a description. Every reporter and patient field is
    /// left empty on purpose — anonymous reporting is a deliberate feature.
    /// </summary>
    private static IncidentRegistrationFormVm MinimalValidForm() => new()
    {
        IsDatePeriod = false,
        Date = DateTime.Today.AddDays(-1),
        Time = "10:30",
        Department = new DepartmentDto { Id = 1, Name = "Oddział wewnętrzny", Code = "OW" },
        SelectedIncidentDefinitions =
        [
            new IncidentDefinitionDto { Id = 1, Name = "Upadek pacjenta", Category = IncidentCategory.Clinical }
        ],
        IncidentDescription = "Pacjent upadł przy łóżku."
    };

    private static bool HasErrorFor(ValidationResult result, string propertyName) =>
        result.Errors.Any(e => e.PropertyName == propertyName);

    [Fact]
    public void AcceptsAnonymousReportWithNoReporterOrPatientDetails()
    {
        var result = _sut.Validate(MinimalValidForm());

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Theory]
    [InlineData("Paweł")]
    [InlineData("Łukasz")]
    [InlineData("Żaneta")]
    [InlineData("Zbigniew")]
    [InlineData("Anna-Maria")]
    [InlineData("O'Brien")]
    [InlineData("Maria Krystyna")]
    public void AcceptsPersonalNamesIncludingPolishDiacriticsAndCompoundForms(string name)
    {
        // Regression guard: the name pattern once spelled the Polish alphabet out as literal
        // characters, so saving this source file as Windows-1250 made it reject "Paweł".
        var form = MinimalValidForm();
        form.Name = name;
        form.Surname = name;

        var result = _sut.Validate(form);

        Assert.False(HasErrorFor(result, nameof(form.Name)));
        Assert.False(HasErrorFor(result, nameof(form.Surname)));
    }

    [Theory]
    [InlineData("Jan3")]
    [InlineData("Jan!")]
    [InlineData("123")]
    public void RejectsNamesContainingDigitsOrPunctuation(string name)
    {
        var form = MinimalValidForm();
        form.Name = name;

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.Name)));
    }

    [Fact]
    public void RejectsNameShorterThanTwoCharacters()
    {
        var form = MinimalValidForm();
        form.Name = "J";

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.Name)));
    }

    [Theory]
    [InlineData("+48123456789")]
    [InlineData("+48 123456789")]
    [InlineData("+48-123456789")]
    [InlineData("123456789")]
    public void AcceptsValidPolishPhoneNumbers(string phone)
    {
        var form = MinimalValidForm();
        form.Phone = phone;

        Assert.False(HasErrorFor(_sut.Validate(form), nameof(form.Phone)));
    }

    [Theory]
    [InlineData("023456789")]
    [InlineData("12345")]
    [InlineData("+1123456789")]
    public void RejectsInvalidPhoneNumbers(string phone)
    {
        var form = MinimalValidForm();
        form.Phone = phone;

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.Phone)));
    }

    [Fact]
    public void RejectsMalformedEmail()
    {
        var form = MinimalValidForm();
        form.Email = "nie-jest-adresem";

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.Email)));
    }

    [Fact]
    public void RejectsPatientDateOfBirthInTheFuture()
    {
        var form = MinimalValidForm();
        form.PatientDob = DateTime.Today.AddDays(1);

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.PatientDob)));
    }

    [Fact]
    public void RejectsPatientDateOfBirthOlderThanTheAgeLimit()
    {
        var form = MinimalValidForm();
        form.PatientDob = DateTime.Today.AddYears(-121);

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.PatientDob)));
    }

    [Fact]
    public void AcceptsPatientDateOfBirthAtTheAgeLimit()
    {
        var form = MinimalValidForm();
        form.PatientDob = DateTime.Today.AddYears(-120);

        Assert.False(HasErrorFor(_sut.Validate(form), nameof(form.PatientDob)));
    }

    [Fact]
    public void RequiresBothEndsOfADateRange()
    {
        var form = MinimalValidForm();
        form.IsDatePeriod = true;
        form.Date = null;
        form.Time = null;

        var result = _sut.Validate(form);

        Assert.True(HasErrorFor(result, nameof(form.DateFrom)));
        Assert.True(HasErrorFor(result, nameof(form.DateTo)));
    }

    [Fact]
    public void AcceptsAWellFormedDateRange()
    {
        var form = MinimalValidForm();
        form.IsDatePeriod = true;
        form.Date = null;
        form.Time = null;
        form.DateFrom = DateTime.Today.AddDays(-5);
        form.DateTo = DateTime.Today.AddDays(-2);

        Assert.True(_sut.Validate(form).IsValid);
    }

    [Fact]
    public void RejectsAnInvertedDateRange()
    {
        var form = MinimalValidForm();
        form.IsDatePeriod = true;
        form.Date = null;
        form.Time = null;
        form.DateFrom = DateTime.Today.AddDays(-2);
        form.DateTo = DateTime.Today.AddDays(-5);

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.DateTo)));
    }

    [Fact]
    public void RejectsADateRangeInTheFuture()
    {
        var form = MinimalValidForm();
        form.IsDatePeriod = true;
        form.Date = null;
        form.Time = null;
        form.DateFrom = DateTime.Today.AddDays(1);
        form.DateTo = DateTime.Today.AddDays(2);

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.DateFrom)));
    }

    [Fact]
    public void RequiresBothDateAndTimeForASinglePointInTime()
    {
        var form = MinimalValidForm();
        form.Date = null;
        form.Time = null;

        var result = _sut.Validate(form);

        Assert.True(HasErrorFor(result, nameof(form.Date)));
        Assert.True(HasErrorFor(result, nameof(form.Time)));
    }

    [Fact]
    public void RejectsAnEventTimedLaterToday()
    {
        // A date-only comparison would let this through: the date is today, but the clock
        // time has not arrived yet.
        var form = MinimalValidForm();
        form.Date = DateTime.Today;
        form.Time = DateTime.Now.AddHours(2).ToString("HH:mm");

        Assert.True(HasErrorFor(_sut.Validate(form), "Time"));
    }

    [Fact]
    public void RequiresADepartment()
    {
        var form = MinimalValidForm();
        form.Department = null;

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.Department)));
    }

    [Fact]
    public void AcceptsAFreeTextEventTypeInsteadOfADictionaryEntry()
    {
        var form = MinimalValidForm();
        form.SelectedIncidentDefinitions = [];
        form.OtherIncidentDefinition = "Nietypowe zdarzenie spoza słownika";

        Assert.True(_sut.Validate(form).IsValid);
    }

    [Fact]
    public void RejectsAFormWithNeitherADictionaryEntryNorFreeText()
    {
        var form = MinimalValidForm();
        form.SelectedIncidentDefinitions = [];
        form.OtherIncidentDefinition = null;

        Assert.True(HasErrorFor(_sut.Validate(form), "SelectedIncidentDefinitions"));
    }

    [Fact]
    public void RequiresADescription()
    {
        var form = MinimalValidForm();
        form.IncidentDescription = "";

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.IncidentDescription)));
    }

    [Fact]
    public void RejectsADescriptionOverTheLengthLimit()
    {
        var form = MinimalValidForm();
        form.IncidentDescription = new string('a', 5001);

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.IncidentDescription)));
    }

    [Fact]
    public async Task ValidateValueReturnsMessagesForTheRequestedPropertyOnly()
    {
        var form = MinimalValidForm();
        form.Name = "Jan3";
        form.IncidentDescription = "";

        var messages = await _sut.ValidateValue(form, nameof(form.Name));

        Assert.Contains("Imię może zawierać tylko litery", messages);
        Assert.DoesNotContain("Opis zdarzenia jest wymagany", messages);
    }
}
```

- [ ] **Step 2: Uruchom testy**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

Oczekiwane: wszystkie przechodzą. Jeśli test z polskimi znakami zawiedzie, sprawdź kodowanie pliku testowego — musi być UTF-8 z BOM.

- [ ] **Step 3: Commit**

```bash
git add SafeCare.Tests/Unit/Validators/IncidentRegistrationFormValidatorTests.cs
git commit -m "Add public incident form validator coverage"
```

---

### Task 4: Testy walidatora konta administracyjnego

**Files:**
- Create: `SafeCare.Tests/Unit/Validators/AdminUserCreateValidatorTests.cs`

**Interfaces:**
- Consumes: projekt z Task 1
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

```csharp
using FluentValidation.Results;
using SafeCare.Data.Entities;
using SafeCare.Validators;
using SafeCare.ViewModels;

namespace SafeCare.Tests.Unit.Validators;

public class AdminUserCreateValidatorTests
{
    private readonly AdminUserCreateValidator _sut = new();

    private static AdminUserCreateVm ValidForm() => new()
    {
        UserName = "nowak",
        FirstName = "Jan",
        LastName = "Nowak",
        Email = "jan.nowak@szpital.pl",
        Password = "Haslo123!",
        ConfirmPassword = "Haslo123!",
        Role = AppRoles.User
    };

    private static bool HasErrorFor(ValidationResult result, string propertyName) =>
        result.Errors.Any(e => e.PropertyName == propertyName);

    [Fact]
    public void AcceptsAWellFormedAccount()
    {
        var result = _sut.Validate(ValidForm());

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void RequiresAUserName()
    {
        var form = ValidForm();
        form.UserName = "";

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.UserName)));
    }

    [Fact]
    public void RejectsAFirstNameOverTwentyCharacters()
    {
        var form = ValidForm();
        form.FirstName = new string('a', 21);

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.FirstName)));
    }

    [Fact]
    public void RejectsALastNameOverThirtyCharacters()
    {
        var form = ValidForm();
        form.LastName = new string('a', 31);

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.LastName)));
    }

    [Fact]
    public void RejectsAMalformedEmail()
    {
        var form = ValidForm();
        form.Email = "nie-jest-adresem";

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.Email)));
    }

    [Fact]
    public void RejectsMismatchedPasswordConfirmation()
    {
        var form = ValidForm();
        form.ConfirmPassword = "CosInnego123!";

        var result = _sut.Validate(form);

        Assert.Contains(result.Errors, e => e.ErrorMessage == "Hasła muszą być takie same");
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.User)]
    public void AcceptsTheTwoKnownRoles(string role)
    {
        var form = ValidForm();
        form.Role = role;

        Assert.False(HasErrorFor(_sut.Validate(form), nameof(form.Role)));
    }

    [Fact]
    public void RejectsAnUnknownRole()
    {
        var form = ValidForm();
        form.Role = "Superadmin";

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.Role)));
    }
}
```

- [ ] **Step 2: Uruchom testy**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

Jeśli nazwy właściwości w `AdminUserCreateVm` różnią się od użytych, dostosuj je — plik jest w `SafeCare/ViewModels/AdminUserCreateVm.cs`.

- [ ] **Step 3: Commit**

```bash
git add SafeCare.Tests/Unit/Validators/AdminUserCreateValidatorTests.cs
git commit -m "Add admin user validator coverage"
```

---

### Task 5: Testy mapowań

Zakres zawężony świadomie względem specyfikacji: pokrywamy `IncidentReportMapping`, bo tylko ono
zawiera logikę — rozstrzyga wariant czasu zdarzenia i rzuca przy brakach. `DepartmentMapping` i
`IncidentDefinitionMapping` przepisują pola jeden do jednego; test na nie powtarzałby inicjalizator
obiektu i psułby się przy każdym dodaniu pola, nie wykrywając niczego.

**Files:**
- Create: `SafeCare.Tests/Unit/Mappings/IncidentReportMappingTests.cs`

**Interfaces:**
- Consumes: projekt z Task 1
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

Mapowanie `ToDto()` rozstrzyga dwa warianty czasu zdarzenia i rzuca przy brakujących danych wymaganych.

```csharp
using SafeCare.Dtos;
using SafeCare.Enums;
using SafeCare.Mappings;
using SafeCare.ViewModels;

namespace SafeCare.Tests.Unit.Mappings;

public class IncidentReportMappingTests
{
    private static IncidentRegistrationFormVm FormWithSingleDate() => new()
    {
        Name = "Anna",
        Surname = "Kowalska",
        Phone = "123456789",
        Email = "anna@example.com",
        PatientName = "Piotr",
        PatientSurname = "Wiśniewski",
        PatientDob = new DateTime(1980, 5, 12),
        PatientGender = Gender.Male,
        IsDatePeriod = false,
        Date = new DateTime(2026, 3, 10),
        Time = "14:45",
        Department = new DepartmentDto { Id = 7, Name = "Chirurgia", Code = "CH" },
        SelectedIncidentDefinitions =
        [
            new IncidentDefinitionDto { Id = 3, Name = "Upadek", Category = IncidentCategory.Clinical }
        ],
        OtherIncidentDefinition = null,
        IncidentDescription = "Opis zdarzenia."
    };

    [Fact]
    public void CopiesEveryReporterAndPatientField()
    {
        var dto = FormWithSingleDate().ToDto();

        Assert.Equal("Anna", dto.Name);
        Assert.Equal("Kowalska", dto.Surname);
        Assert.Equal("123456789", dto.Phone);
        Assert.Equal("anna@example.com", dto.Email);
        Assert.Equal("Piotr", dto.PatientName);
        Assert.Equal("Wiśniewski", dto.PatientSurname);
        Assert.Equal(new DateTime(1980, 5, 12), dto.PatientDob);
        Assert.Equal(Gender.Male, dto.PatientGender);
        Assert.Equal(7, dto.Department.Id);
        Assert.Equal("Opis zdarzenia.", dto.IncidentDescription);
        Assert.Single(dto.SelectedIncidentDefinitions);
    }

    [Fact]
    public void CombinesDateAndTimeIntoASingleTimestamp()
    {
        var dto = FormWithSingleDate().ToDto();

        Assert.Equal(new DateTime(2026, 3, 10, 14, 45, 0), dto.Date);
        Assert.Null(dto.DateFrom);
        Assert.Null(dto.DateTo);
    }

    [Fact]
    public void KeepsOnlyTheRangeWhenTheFormIsInPeriodMode()
    {
        var form = FormWithSingleDate();
        form.IsDatePeriod = true;
        form.DateFrom = new DateTime(2026, 3, 1, 9, 30, 0);
        form.DateTo = new DateTime(2026, 3, 5, 18, 0, 0);

        var dto = form.ToDto();

        Assert.Equal(new DateTime(2026, 3, 1), dto.DateFrom);
        Assert.Equal(new DateTime(2026, 3, 5), dto.DateTo);
        Assert.Null(dto.Date);
    }

    [Fact]
    public void FallsBackToNotProvidedWhenGenderIsAbsent()
    {
        var form = FormWithSingleDate();
        form.PatientGender = null;

        Assert.Equal(Gender.NotProvided, form.ToDto().PatientGender);
    }

    [Fact]
    public void ThrowsWhenTheTimeCannotBeParsed()
    {
        var form = FormWithSingleDate();
        form.Time = "nie-godzina";

        Assert.Throws<ArgumentException>(() => form.ToDto());
    }

    [Fact]
    public void ThrowsWhenTheDepartmentIsMissing()
    {
        var form = FormWithSingleDate();
        form.Department = null;

        Assert.Throws<ArgumentNullException>(() => form.ToDto());
    }

    [Fact]
    public void ThrowsWhenTheDescriptionIsBlank()
    {
        var form = FormWithSingleDate();
        form.IncidentDescription = "   ";

        Assert.Throws<ArgumentNullException>(() => form.ToDto());
    }
}
```

- [ ] **Step 2: Uruchom testy i commituj**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

```bash
git add SafeCare.Tests/Unit/Mappings/IncidentReportMappingTests.cs
git commit -m "Add incident report mapping coverage"
```

---

### Task 6: Testy etykiet i ikon enumów

**Files:**
- Create: `SafeCare.Tests/Unit/Enums/EnumDisplayTests.cs`

**Interfaces:**
- Consumes: projekt z Task 1
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

Test przebiega po **wszystkich** wartościach enumów, więc dodanie nowej bez etykiety lub bez ikony zapali się samo.

```csharp
using MudBlazor;
using SafeCare.Enums;
using SafeCare.Utils;

namespace SafeCare.Tests.Unit.Enums;

public class EnumDisplayTests
{
    public static TheoryData<IncidentCategory> AllCategories() => [.. Enum.GetValues<IncidentCategory>()];

    public static TheoryData<ReportStatus> AllStatuses() => [.. Enum.GetValues<ReportStatus>()];

    public static TheoryData<Gender> AllGenders() => [.. Enum.GetValues<Gender>()];

    [Theory]
    [MemberData(nameof(AllCategories))]
    public void EveryCategoryCarriesAPolishLabel(IncidentCategory category)
    {
        var label = category.GetDisplayName();

        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.NotEqual(category.ToString(), label);
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void EveryStatusCarriesAPolishLabel(ReportStatus status)
    {
        var label = status.GetDisplayName();

        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.NotEqual(status.ToString(), label);
    }

    [Theory]
    [MemberData(nameof(AllGenders))]
    public void EveryGenderCarriesAPolishLabel(Gender gender)
    {
        var label = gender.GetDisplayName();

        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.NotEqual(gender.ToString(), label);
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void EveryStatusMapsToADedicatedColour(ReportStatus status)
    {
        // Color.Default is the switch's fallback arm — reaching it means a status was added
        // without extending the mapping.
        Assert.NotEqual(Color.Default, status.GetColor());
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void EveryStatusMapsToAnIcon(ReportStatus status)
    {
        Assert.False(string.IsNullOrEmpty(status.GetIcon()));
    }

    [Fact]
    public void OtherCategoryUsesTheLabelTheGridAndFilterShareAsAContract()
    {
        // The dashboard chip and the category filter both key off this exact label.
        Assert.Equal("Inne", IncidentCategory.Other.GetDisplayName());
    }
}
```

- [ ] **Step 2: Uruchom testy i commituj**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

```bash
git add SafeCare.Tests/Unit/Enums/EnumDisplayTests.cs
git commit -m "Add enum display metadata coverage"
```

---

### Task 7: Testy szablonu e-mail

**Files:**
- Create: `SafeCare.Tests/Unit/Email/IncidentEmailTemplateTests.cs`

**Interfaces:**
- Consumes: projekt z Task 1
- Produces: `ReportFactory.Create(...)` — pomocnik budujący encję `IncidentReport`; używany ponownie w Task 8

- [ ] **Step 1: Utwórz pomocnika budującego encję**

Plik `SafeCare.Tests/Unit/ReportFactory.cs`. `IncidentReport` ma konstruktor walidujący daty, więc pomocnik musi podawać sensowną przeszłą datę.

```csharp
using SafeCare.Data.Entities;
using SafeCare.Enums;

namespace SafeCare.Tests.Unit;

/// <summary>
/// Builds valid <see cref="IncidentReport"/> entities for tests. The entity constructor
/// enforces the date rules, so every field it validates gets a sane default here.
/// </summary>
public static class ReportFactory
{
    public static Department Department(string name = "Chirurgia", string code = "CH") =>
        new() { Name = name, Code = code };

    public static IncidentDefinition Definition(
        string name = "Upadek pacjenta",
        IncidentCategory category = IncidentCategory.Clinical) =>
        new() { Name = name, Category = category };

    public static IncidentReport Create(
        Department? department = null,
        IList<IncidentDefinition>? definitions = null,
        string? name = "Anna",
        string? surname = "Kowalska",
        string? patientName = "Piotr",
        string? patientSurname = "Wiśniewski",
        DateTime? patientDob = null,
        Gender gender = Gender.Male,
        DateTime? date = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string? otherIncidentDefinition = null,
        string description = "Opis zdarzenia.") =>
        new(
            name,
            surname,
            "123456789",
            "anna@example.com",
            patientName,
            patientSurname,
            patientDob ?? new DateTime(1980, 5, 12),
            gender,
            dateFrom,
            dateTo,
            dateFrom is null && dateTo is null ? date ?? DateTime.Now.AddDays(-1) : null,
            department ?? Department(),
            definitions ?? [Definition()],
            otherIncidentDefinition,
            description);
}
```

- [ ] **Step 2: Napisz testy szablonu**

```csharp
using SafeCare.Email;

namespace SafeCare.Tests.Unit.Email;

public class IncidentEmailTemplateTests
{
    [Fact]
    public void AddressesEveryRecipientThroughBcc()
    {
        // Staff addresses must not be exposed to one another.
        var recipients = new[] { "a@szpital.pl", "b@szpital.pl" };

        var message = IncidentEmailTemplate.Build(ReportFactory.Create(), recipients);

        Assert.Equal(recipients, message.BccRecipients);
    }

    [Fact]
    public void PutsTheReportNumberInTheSubject()
    {
        var report = ReportFactory.Create();
        report.Id = 42;

        var message = IncidentEmailTemplate.Build(report, ["a@szpital.pl"]);

        Assert.Contains("#42", message.Subject);
    }

    [Fact]
    public void IncludesTheDepartmentAndDescriptionInTheBody()
    {
        var report = ReportFactory.Create(
            department: ReportFactory.Department("Oddział wewnętrzny", "OW"),
            description: "Pacjent zgłosił ból po podaniu leku.");

        var message = IncidentEmailTemplate.Build(report, ["a@szpital.pl"]);

        Assert.Contains("Oddział wewnętrzny", message.HtmlBody);
        Assert.Contains("Pacjent zgłosił ból po podaniu leku.", message.HtmlBody);
    }

    [Fact]
    public void EncodesMarkupSuppliedByTheReporter()
    {
        // The description is free text typed by an anonymous member of the public.
        var report = ReportFactory.Create(description: "<script>alert('x')</script>");

        var message = IncidentEmailTemplate.Build(report, ["a@szpital.pl"]);

        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
    }

    [Fact]
    public void RendersADateRangeWhenTheEventSpansAPeriod()
    {
        var report = ReportFactory.Create(
            dateFrom: DateTime.Today.AddDays(-5),
            dateTo: DateTime.Today.AddDays(-2));

        var message = IncidentEmailTemplate.Build(report, ["a@szpital.pl"]);

        Assert.Contains(DateTime.Today.AddDays(-5).ToString("dd.MM.yyyy"), message.HtmlBody);
        Assert.Contains(DateTime.Today.AddDays(-2).ToString("dd.MM.yyyy"), message.HtmlBody);
    }
}
```

- [ ] **Step 3: Uruchom testy i commituj**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

```bash
git add SafeCare.Tests/Unit/ReportFactory.cs SafeCare.Tests/Unit/Email/IncidentEmailTemplateTests.cs
git commit -m "Add incident email template coverage"
```

---

### Task 8: Testy walidacji dat w encji IncidentReport

Encja sama pilnuje reguły „dokładna data albo kompletny zakres, nigdy w przyszłości". To czysta logika domenowa, broniąca również ścieżek omijających formularz.

**Files:**
- Create: `SafeCare.Tests/Unit/Entities/IncidentReportTests.cs`

**Interfaces:**
- Consumes: `ReportFactory` z Task 7
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

```csharp
using SafeCare.Exceptions;

namespace SafeCare.Tests.Unit.Entities;

public class IncidentReportTests
{
    [Fact]
    public void AcceptsAnExactPointInTime()
    {
        var report = ReportFactory.Create(date: DateTime.Now.AddHours(-3));

        Assert.NotNull(report.Date);
        Assert.Null(report.DateFrom);
    }

    [Fact]
    public void AcceptsACompleteRange()
    {
        var report = ReportFactory.Create(
            dateFrom: DateTime.Today.AddDays(-5),
            dateTo: DateTime.Today.AddDays(-2));

        Assert.Equal(DateTime.Today.AddDays(-5), report.DateFrom);
        Assert.Equal(DateTime.Today.AddDays(-2), report.DateTo);
    }

    [Fact]
    public void StripsTheTimeComponentFromRangeEnds()
    {
        var report = ReportFactory.Create(
            dateFrom: DateTime.Today.AddDays(-5).AddHours(13),
            dateTo: DateTime.Today.AddDays(-2).AddHours(17));

        Assert.Equal(DateTime.Today.AddDays(-5), report.DateFrom);
        Assert.Equal(DateTime.Today.AddDays(-2), report.DateTo);
    }

    [Fact]
    public void RejectsAHalfOpenRangeWithNoExactDate()
    {
        var exception = Assert.Throws<DomainException>(() =>
            ReportFactory.Create(dateFrom: DateTime.Today.AddDays(-5), dateTo: null, date: null));

        Assert.Contains("zakres dat", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAnInvertedRange()
    {
        Assert.Throws<DomainException>(() =>
            ReportFactory.Create(
                dateFrom: DateTime.Today.AddDays(-2),
                dateTo: DateTime.Today.AddDays(-5)));
    }

    [Fact]
    public void RejectsARangeInTheFuture()
    {
        Assert.Throws<DomainException>(() =>
            ReportFactory.Create(
                dateFrom: DateTime.Today.AddDays(1),
                dateTo: DateTime.Today.AddDays(2)));
    }

    [Fact]
    public void RejectsAnExactDateInTheFuture()
    {
        Assert.Throws<DomainException>(() => ReportFactory.Create(date: DateTime.Now.AddHours(2)));
    }

    [Fact]
    public void StampsNewReportsAsNew()
    {
        var report = ReportFactory.Create();

        Assert.Equal(SafeCare.Enums.ReportStatus.New, report.Status);
    }
}
```

- [ ] **Step 2: Uruchom testy i commituj**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

```bash
git add SafeCare.Tests/Unit/Entities/IncidentReportTests.cs
git commit -m "Add incident report date rule coverage"
```

---

### Task 9: Strażnik kodowania plików źródłowych

Trzy pliki zapisane kiedyś jako Windows-1250 przeszły kompilację i rozsypały cały formularz publiczny. Ten test wyłapuje nawrót tej klasy awarii.

**Files:**
- Create: `SafeCare.Tests/Unit/SourceEncodingTests.cs`

**Interfaces:**
- Consumes: projekt z Task 1
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz test**

```csharp
using System.Text;

namespace SafeCare.Tests.Unit;

/// <summary>
/// Guards against source files being saved in a single-byte codepage. Windows-1250 bytes are
/// valid on disk but decode as replacement characters when the compiler reads them as UTF-8,
/// which once silently broke every Polish label on the public form.
/// </summary>
public class SourceEncodingTests
{
    private static DirectoryInfo RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SafeCare.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!;
    }

    public static TheoryData<string> SourceFiles()
    {
        var root = RepositoryRoot();
        var data = new TheoryData<string>();

        foreach (var file in root.EnumerateFiles("*.*", SearchOption.AllDirectories))
        {
            if (file.Extension is not (".cs" or ".razor"))
            {
                continue;
            }

            var relative = Path.GetRelativePath(root.FullName, file.FullName);

            if (relative.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || relative.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            data.Add(relative);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void SourceFileIsValidUtf8(string relativePath)
    {
        var fullPath = Path.Combine(RepositoryRoot().FullName, relativePath);
        var bytes = File.ReadAllBytes(fullPath);

        // Throw-on-invalid decoders turn a mis-encoded byte sequence into an exception
        // instead of silently substituting U+FFFD.
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        var exception = Record.Exception(() => strictUtf8.GetString(bytes));

        Assert.Null(exception);
    }

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void SourceFileContainsNoReplacementCharacter(string relativePath)
    {
        var fullPath = Path.Combine(RepositoryRoot().FullName, relativePath);

        Assert.DoesNotContain('�', File.ReadAllText(fullPath));
    }
}
```

- [ ] **Step 2: Uruchom testy**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

Oczekiwane: wszystkie pliki przechodzą. **Jeśli któryś zawiedzie — to prawdziwe znalezisko, nie usterka testu.** Zgłoś plik i przekonwertuj go na UTF-8, zamiast osłabiać asercję.

- [ ] **Step 3: Commit**

```bash
git add SafeCare.Tests/Unit/SourceEncodingTests.cs
git commit -m "Add source file encoding guard"
```

---

### Task 10: Infrastruktura testów integracyjnych + pierwszy test CreateReport

**Files:**
- Create: `SafeCare.Tests/Integration/Infrastructure/PostgresFixture.cs`
- Create: `SafeCare.Tests/Integration/Infrastructure/TestDbContextFactory.cs`
- Create: `SafeCare.Tests/Integration/Infrastructure/FakeEmailQueue.cs`
- Create: `SafeCare.Tests/Integration/Infrastructure/IntegrationTestBase.cs`
- Create: `SafeCare.Tests/Integration/IncidentReportServiceCreateTests.cs`

**Interfaces:**
- Consumes: projekt z Task 1
- Produces:
  - `PostgresFixture` z `string ConnectionString`, `DbContextOptions<AppDbContext> Options`, `Task ResetAsync()`
  - `TestDbContextFactory(DbContextOptions<AppDbContext>)` implementujące `IDbContextFactory<AppDbContext>`
  - `FakeEmailQueue` z `IReadOnlyList<EmailMessage> Sent`, `bool ThrowOnEnqueue`
  - `IntegrationTestBase` z `Fixture`, `DbFactory`, `EmailQueue`, `CreateDbContext()`, `SeedDepartmentAsync(...)`, `SeedDefinitionAsync(...)`
  - `[Collection(PostgresCollection.Name)]` jako sposób podpięcia współdzielonego kontenera

- [ ] **Step 1: Napisz `PostgresFixture`**

Jeden kontener na cały przebieg, migracje raz, role zasiane raz. `DbSeeder` **nie** jest tu uruchamiany — testy tworzą własne dane, a Task 16 sprawdza sam seeder na czystej bazie.

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SafeCare.Data;
using SafeCare.Data.Entities;
using Testcontainers.PostgreSql;

namespace SafeCare.Tests.Integration.Infrastructure;

/// <summary>
/// Owns the PostgreSQL container shared by every integration test. Migrations are expensive,
/// so they run once for the whole session; isolation between tests comes from
/// <see cref="ResetAsync"/> emptying the tables instead.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Seeded through EF HasData in the initial migration; must survive cleanup.</summary>
    public static readonly Guid SeededAdminId = Guid.Parse("62228aa3-8032-4d31-8b99-719629d26bb7");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public DbContextOptions<AppDbContext> Options { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new AppDbContext(Options);
        await dbContext.Database.MigrateAsync();

        await SeedRolesAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Empties the domain tables and every account except the seeded administrator, which
    /// cannot be recreated without re-running the migration that carries it.
    /// </summary>
    public async Task ResetAsync()
    {
        const string sql = """
            TRUNCATE TABLE "IncidentDefinitionIncidentReport", "IncidentReports" RESTART IDENTITY CASCADE;
            TRUNCATE TABLE "Departments" RESTART IDENTITY CASCADE;
            TRUNCATE TABLE "IncidentDefinitions" RESTART IDENTITY CASCADE;
            DELETE FROM "AspNetUserRoles" WHERE "UserId" <> @adminId;
            DELETE FROM "AspNetUsers" WHERE "Id" <> @adminId;
            """;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("adminId", SeededAdminId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Creates the application roles and gives the seeded administrator its role, mirroring
    /// what <see cref="IdentitySeeder"/> does at application startup.
    /// </summary>
    private async Task SeedRolesAsync()
    {
        var provider = IdentityServiceProvider.Build(Options);
        using var scope = provider.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        await IdentitySeeder.SeedRolesAndAdminAsync(roleManager, userManager);
    }
}

[CollectionDefinition(PostgresCollection.Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
```

- [ ] **Step 2: Napisz `IdentityServiceProvider`**

Plik `SafeCare.Tests/Integration/Infrastructure/IdentityServiceProvider.cs`.

Uwaga na świadome odstępstwo: produkcyjny `IdentityConfig.AddIdentity()` dokłada ciasteczka uwierzytelniające, których testy serwisowe nie potrzebują i które wymagałyby pełnego potoku HTTP. Powtarzamy tu wyłącznie rejestrację rdzenia Identity. Polityka haseł jest identyczna, bo `IdentityConfig` nie zmienia jej ustawień — korzysta z domyślnych.

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SafeCare.Data;
using SafeCare.Data.Entities;

namespace SafeCare.Tests.Integration.Infrastructure;

/// <summary>
/// Builds a minimal service provider carrying a real <see cref="UserManager{TUser}"/> backed by
/// the test database, so account rules are exercised against genuine Identity behaviour.
/// </summary>
public static class IdentityServiceProvider
{
    public static ServiceProvider Build(DbContextOptions<AppDbContext> options)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        // AddEntityFrameworkStores needs a scoped context; the "always use the factory" rule
        // guards Blazor circuits, which do not exist here.
        services.AddScoped(_ => new AppDbContext(options));
        services.AddSingleton<IDbContextFactory<AppDbContext>>(new TestDbContextFactory(options));

        services.AddIdentityCore<User>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        return services.BuildServiceProvider();
    }
}
```

- [ ] **Step 3: Napisz `TestDbContextFactory` i `FakeEmailQueue`**

`SafeCare.Tests/Integration/Infrastructure/TestDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using SafeCare.Data;

namespace SafeCare.Tests.Integration.Infrastructure;

/// <summary>
/// Hands out short-lived contexts exactly the way the application's factory does, so services
/// are exercised through the same contract they use in production.
/// </summary>
public sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
    : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);
}
```

`SafeCare.Tests/Integration/Infrastructure/FakeEmailQueue.cs`:

```csharp
using System.Runtime.CompilerServices;
using SafeCare.Email;

namespace SafeCare.Tests.Integration.Infrastructure;

/// <summary>
/// Records what would have been sent. <see cref="ThrowOnEnqueue"/> simulates a broken mail
/// pipeline, which must never fail the report being submitted.
/// </summary>
public sealed class FakeEmailQueue : IEmailQueue
{
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> Sent => _sent;

    public bool ThrowOnEnqueue { get; set; }

    public void Enqueue(EmailMessage message)
    {
        if (ThrowOnEnqueue)
        {
            throw new InvalidOperationException("Simulated mail queue failure");
        }

        _sent.Add(message);
    }

    public async IAsyncEnumerable<EmailMessage> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }
}
```

- [ ] **Step 4: Napisz `IntegrationTestBase`**

```csharp
using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.Data.Entities;
using SafeCare.Enums;

namespace SafeCare.Tests.Integration.Infrastructure;

/// <summary>
/// Shared setup for database-backed tests: a clean schema per test, a context factory and a
/// recording mail queue.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public abstract class IntegrationTestBase(PostgresFixture fixture) : IAsyncLifetime
{
    protected PostgresFixture Fixture { get; } = fixture;

    protected TestDbContextFactory DbFactory { get; } = new(fixture.Options);

    protected FakeEmailQueue EmailQueue { get; } = new();

    public async ValueTask InitializeAsync() => await Fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected AppDbContext CreateDbContext() => DbFactory.CreateDbContext();

    protected async Task<Department> SeedDepartmentAsync(string name = "Chirurgia", string code = "CH")
    {
        await using var db = CreateDbContext();
        var department = new Department { Name = name, Code = code };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        return department;
    }

    protected async Task<IncidentDefinition> SeedDefinitionAsync(
        string name = "Upadek pacjenta",
        IncidentCategory category = IncidentCategory.Clinical)
    {
        await using var db = CreateDbContext();
        var definition = new IncidentDefinition { Name = name, Category = category };
        db.IncidentDefinitions.Add(definition);
        await db.SaveChangesAsync();
        return definition;
    }

    /// <summary>Persists a report directly, bypassing the service under test.</summary>
    protected async Task<IncidentReport> SeedReportAsync(
        Department department,
        IList<IncidentDefinition>? definitions = null,
        string? name = "Anna",
        string? surname = "Kowalska",
        string? patientName = "Piotr",
        string? patientSurname = "Wiśniewski",
        DateTime? patientDob = null,
        Gender gender = Gender.Male,
        DateTime? date = null,
        string? otherIncidentDefinition = null,
        string description = "Opis zdarzenia.",
        ReportStatus status = ReportStatus.New)
    {
        await using var db = CreateDbContext();

        var attachedDepartment = await db.Departments.FirstAsync(d => d.Id == department.Id);
        var attachedDefinitions = definitions is null
            ? []
            : await db.IncidentDefinitions
                .Where(d => definitions.Select(x => x.Id).Contains(d.Id))
                .ToListAsync();

        var report = new IncidentReport(
            name, surname, "123456789", "anna@example.com",
            patientName, patientSurname, patientDob ?? new DateTime(1980, 5, 12), gender,
            null, null, date ?? DateTime.Now.AddDays(-1),
            attachedDepartment, attachedDefinitions, otherIncidentDefinition, description)
        {
            Status = status
        };

        db.IncidentReports.Add(report);
        await db.SaveChangesAsync();
        return report;
    }
}
```

- [ ] **Step 5: Napisz pierwsze testy `CreateReport`**

```csharp
using Microsoft.EntityFrameworkCore;
using SafeCare.Dtos;
using SafeCare.Enums;
using SafeCare.Exceptions;
using SafeCare.Mappings;
using SafeCare.Services;
using SafeCare.Tests.Integration.Infrastructure;

namespace SafeCare.Tests.Integration;

public class IncidentReportServiceCreateTests(PostgresFixture fixture) : IntegrationTestBase(fixture)
{
    private IncidentReportService CreateSut() => new(DbFactory, EmailQueue);

    private static IncidentReportDto BuildDto(
        DepartmentDto department,
        IList<IncidentDefinitionDto> definitions,
        string? other = null) => new()
    {
        Name = "Anna",
        Surname = "Kowalska",
        PatientGender = Gender.Female,
        Date = DateTime.Now.AddDays(-1),
        Department = department,
        SelectedIncidentDefinitions = definitions,
        OtherIncidentDefinition = other,
        IncidentDescription = "Pacjent upadł przy łóżku."
    };

    [Fact]
    public async Task PersistsAReportWithItsDepartmentAndEventTypes()
    {
        var department = await SeedDepartmentAsync();
        var definition = await SeedDefinitionAsync();

        var id = await CreateSut().CreateReport(BuildDto(
            department.ToDto(),
            [definition.ToDto()]));

        await using var db = CreateDbContext();
        var stored = await db.IncidentReports
            .Include(r => r.Department)
            .Include(r => r.IncidentDefinitions)
            .SingleAsync(r => r.Id == id);

        Assert.Equal(department.Id, stored.Department.Id);
        Assert.Single(stored.IncidentDefinitions);
        Assert.Equal(definition.Id, stored.IncidentDefinitions[0].Id);
        Assert.Equal("Pacjent upadł przy łóżku.", stored.IncidentDescription);
        Assert.Equal(ReportStatus.New, stored.Status);
    }

    [Fact]
    public async Task RejectsAReportForAnUnknownDepartment()
    {
        var definition = await SeedDefinitionAsync();
        var missingDepartment = new DepartmentDto { Id = 9999, Name = "Nieistniejący", Code = "XX" };

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            CreateSut().CreateReport(BuildDto(missingDepartment, [definition.ToDto()])));
    }

    [Fact]
    public async Task RejectsAReportReferencingAnUnknownEventType()
    {
        var department = await SeedDepartmentAsync();
        var missingDefinition = new IncidentDefinitionDto
        {
            Id = 9999,
            Name = "Nieistniejące",
            Category = IncidentCategory.Clinical
        };

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            CreateSut().CreateReport(BuildDto(department.ToDto(), [missingDefinition])));
    }

    [Fact]
    public async Task AcceptsAReportDescribedOnlyByFreeText()
    {
        var department = await SeedDepartmentAsync();

        var id = await CreateSut().CreateReport(BuildDto(
            department.ToDto(),
            [],
            other: "Zdarzenie spoza słownika"));

        await using var db = CreateDbContext();
        var stored = await db.IncidentReports.SingleAsync(r => r.Id == id);

        Assert.Equal("Zdarzenie spoza słownika", stored.OtherIncidentDefinition);
        Assert.Empty(await db.Entry(stored).Collection(r => r.IncidentDefinitions).Query().ToListAsync());
    }
}
```

Jeśli `department.ToDto()` lub `definition.ToDto()` nie istnieją w `Mappings/`, zbuduj DTO ręcznie przez inicjalizator obiektu — kształt obu typów jest w `SafeCare/Dtos/`.

- [ ] **Step 6: Uruchom testy**

Docker musi działać.

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

Pierwszy przebieg pobierze obraz `postgres:17-alpine` — może potrwać minutę.

- [ ] **Step 7: Sprawdź, że szybki zestaw nadal działa bez Dockera**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj -- --filter-not-trait "Category=Integration"
```

Oczekiwane: same testy jednostkowe, bez startu kontenera.

- [ ] **Step 8: Commit**

```bash
git add SafeCare.Tests/Integration
git commit -m "Add integration test infrastructure and report creation coverage"
```

---

### Task 11: Testy potoku powiadomień w CreateReport

Tu żyje udokumentowana decyzja projektowa: awaria poczty nie może wywalić zgłoszenia pacjenta.

**Files:**
- Create: `SafeCare.Tests/Integration/IncidentReportServiceNotificationTests.cs`

**Interfaces:**
- Consumes: `IntegrationTestBase`, `FakeEmailQueue` z Task 10
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

```csharp
using Microsoft.EntityFrameworkCore;
using SafeCare.Data.Entities;
using SafeCare.Dtos;
using SafeCare.Enums;
using SafeCare.Mappings;
using SafeCare.Services;
using SafeCare.Tests.Integration.Infrastructure;

namespace SafeCare.Tests.Integration;

public class IncidentReportServiceNotificationTests(PostgresFixture fixture) : IntegrationTestBase(fixture)
{
    private IncidentReportService CreateSut() => new(DbFactory, EmailQueue);

    private async Task AddUserAsync(string userName, string? email, bool receivesNotifications)
    {
        await using var db = CreateDbContext();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email?.ToUpperInvariant(),
            FirstName = "Test",
            LastName = "User",
            ReceiveEmailNotifications = receivesNotifications,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> SubmitReportAsync()
    {
        var department = await SeedDepartmentAsync();
        var definition = await SeedDefinitionAsync();

        return await CreateSut().CreateReport(new IncidentReportDto
        {
            PatientGender = Gender.Female,
            Date = DateTime.Now.AddDays(-1),
            Department = department.ToDto(),
            SelectedIncidentDefinitions = [definition.ToDto()],
            IncidentDescription = "Opis zdarzenia."
        });
    }

    [Fact]
    public async Task NotifiesOnlyUsersWhoOptedIn()
    {
        await AddUserAsync("optin", "optin@szpital.pl", receivesNotifications: true);
        await AddUserAsync("optout", "optout@szpital.pl", receivesNotifications: false);

        await SubmitReportAsync();

        var message = Assert.Single(EmailQueue.Sent);
        Assert.Equal(["optin@szpital.pl"], message.BccRecipients);
    }

    [Fact]
    public async Task SkipsOptedInUsersWithoutAnEmailAddress()
    {
        await AddUserAsync("noaddress", null, receivesNotifications: true);

        await SubmitReportAsync();

        Assert.Empty(EmailQueue.Sent);
    }

    [Fact]
    public async Task SendsNothingWhenNobodyOptedIn()
    {
        await AddUserAsync("optout", "optout@szpital.pl", receivesNotifications: false);

        await SubmitReportAsync();

        Assert.Empty(EmailQueue.Sent);
    }

    [Fact]
    public async Task StillStoresTheReportWhenTheMailQueueFails()
    {
        // Deliberate design decision: "a broken mail server must not fail a patient's report".
        // If this test starts failing, the swallow-and-log in CreateReport was removed.
        await AddUserAsync("optin", "optin@szpital.pl", receivesNotifications: true);
        EmailQueue.ThrowOnEnqueue = true;

        var id = await SubmitReportAsync();

        await using var db = CreateDbContext();
        Assert.True(await db.IncidentReports.AnyAsync(r => r.Id == id));
    }
}
```

- [ ] **Step 2: Uruchom testy i commituj**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj -- --filter-trait "Category=Integration"
```

```bash
git add SafeCare.Tests/Integration/IncidentReportServiceNotificationTests.cs
git commit -m "Add report notification pipeline coverage"
```

---

### Task 12: Testy filtrowania w GetReports

**Files:**
- Create: `SafeCare.Tests/Integration/IncidentReportServiceFilterTests.cs`

**Interfaces:**
- Consumes: `IntegrationTestBase` z Task 10
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

Reguła kategorii „Inne" testowana jest w obie strony — to ona cicho psuła filtr.

```csharp
using SafeCare.Enums;
using SafeCare.Services;
using SafeCare.Tests.Integration.Infrastructure;
using SafeCare.ViewModels;

namespace SafeCare.Tests.Integration;

public class IncidentReportServiceFilterTests(PostgresFixture fixture) : IntegrationTestBase(fixture)
{
    private IncidentReportService CreateSut() => new(DbFactory, EmailQueue);

    private static IncidentReportsRequestVm Request(IncidentReportFilter filter) =>
        new() { Filter = filter, Page = 0, PageSize = 50 };

    [Fact]
    public async Task MatchesReporterNameCaseInsensitivelyAcrossFirstAndLastName()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, name: "Anna", surname: "Kowalska");
        await SeedReportAsync(department, name: "Piotr", surname: "Nowak");

        var result = await CreateSut().GetReports(Request(new IncidentReportFilter { FullName = "anna kow" }));

        Assert.Equal(1, result.ItemTotalCount);
        Assert.Equal("Anna Kowalska", result.Items.Single().FullName);
    }

    [Fact]
    public async Task MatchesNamesContainingPolishDiacritics()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, name: "Paweł", surname: "Wiśniewski");

        var result = await CreateSut().GetReports(Request(new IncidentReportFilter { FullName = "paweł" }));

        Assert.Equal(1, result.ItemTotalCount);
    }

    [Fact]
    public async Task MatchesPatientName()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, patientName: "Marek", patientSurname: "Zieliński");
        await SeedReportAsync(department, patientName: "Ewa", patientSurname: "Dąbrowska");

        var result = await CreateSut().GetReports(
            Request(new IncidentReportFilter { PatientFullName = "zieliń" }));

        Assert.Equal(1, result.ItemTotalCount);
    }

    [Fact]
    public async Task FiltersByPatientGender()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, gender: Gender.Female);
        await SeedReportAsync(department, gender: Gender.Male);

        var result = await CreateSut().GetReports(
            Request(new IncidentReportFilter { Gender = Gender.Female }));

        Assert.Equal(1, result.ItemTotalCount);
    }

    [Fact]
    public async Task FiltersByDepartmentName()
    {
        var surgery = await SeedDepartmentAsync("Chirurgia", "CH");
        var internalWard = await SeedDepartmentAsync("Oddział wewnętrzny", "OW");
        await SeedReportAsync(surgery);
        await SeedReportAsync(internalWard);

        var result = await CreateSut().GetReports(
            Request(new IncidentReportFilter { Department = "chirur" }));

        Assert.Equal(1, result.ItemTotalCount);
        Assert.Equal("Chirurgia", result.Items.Single().Department);
    }

    [Fact]
    public async Task FiltersByStatus()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, status: ReportStatus.New);
        await SeedReportAsync(department, status: ReportStatus.Resolved);

        var result = await CreateSut().GetReports(
            Request(new IncidentReportFilter { Statuses = [ReportStatus.Resolved] }));

        Assert.Equal(1, result.ItemTotalCount);
    }

    [Fact]
    public async Task FiltersByDictionaryCategory()
    {
        var department = await SeedDepartmentAsync();
        var clinical = await SeedDefinitionAsync("Upadek", IncidentCategory.Clinical);
        var pharma = await SeedDefinitionAsync("Zła dawka", IncidentCategory.Pharmacotherapy);
        await SeedReportAsync(department, [clinical]);
        await SeedReportAsync(department, [pharma]);

        var result = await CreateSut().GetReports(
            Request(new IncidentReportFilter { Categories = [IncidentCategory.Pharmacotherapy] }));

        Assert.Equal(1, result.ItemTotalCount);
    }

    [Fact]
    public async Task ReturnsFreeTextReportsWhenFilteringByTheOtherCategory()
    {
        // "Other" has no IncidentDefinitions rows — membership is decided by
        // OtherIncidentDefinition being non-empty.
        var department = await SeedDepartmentAsync();
        var clinical = await SeedDefinitionAsync("Upadek", IncidentCategory.Clinical);
        await SeedReportAsync(department, [clinical]);
        await SeedReportAsync(department, [], otherIncidentDefinition: "Coś nietypowego");

        var result = await CreateSut().GetReports(
            Request(new IncidentReportFilter { Categories = [IncidentCategory.Other] }));

        Assert.Equal(1, result.ItemTotalCount);
        Assert.True(result.Items.Single().HasOtherCategory);
    }

    [Fact]
    public async Task ExcludesFreeTextReportsWhenFilteringByADictionaryCategory()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, [], otherIncidentDefinition: "Coś nietypowego");

        var result = await CreateSut().GetReports(
            Request(new IncidentReportFilter { Categories = [IncidentCategory.Clinical] }));

        Assert.Equal(0, result.ItemTotalCount);
    }

    [Fact]
    public async Task TreatsWildcardCharactersInFilterTermsLiterally()
    {
        // Without escaping, "100%" would match every row.
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, name: "100%", surname: "Pewny");
        await SeedReportAsync(department, name: "Anna", surname: "Kowalska");

        var result = await CreateSut().GetReports(
            Request(new IncidentReportFilter { FullName = "100%" }));

        Assert.Equal(1, result.ItemTotalCount);
    }

    [Fact]
    public async Task AppendsTheOtherChipToReportsCarryingFreeText()
    {
        var department = await SeedDepartmentAsync();
        var clinical = await SeedDefinitionAsync("Upadek", IncidentCategory.Clinical);
        await SeedReportAsync(department, [clinical], otherIncidentDefinition: "Dodatkowy opis");

        var result = await CreateSut().GetReports(Request(new IncidentReportFilter()));

        var item = result.Items.Single();
        Assert.Contains(IncidentCategory.Clinical, item.Categories);
        Assert.Contains(IncidentCategory.Other, item.Categories);
    }
}
```

- [ ] **Step 2: Uruchom testy i commituj**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj -- --filter-trait "Category=Integration"
```

```bash
git add SafeCare.Tests/Integration/IncidentReportServiceFilterTests.cs
git commit -m "Add report grid filtering coverage"
```

---

### Task 13: Testy sortowania i stronicowania w GetReports

Sortowanie zostało kiedyś odwrócone na wszystkich kolumnach naraz. Te testy przybijają kierunek każdej z nich.

**Files:**
- Create: `SafeCare.Tests/Integration/IncidentReportServiceSortingTests.cs`

**Interfaces:**
- Consumes: `IntegrationTestBase` z Task 10
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

```csharp
using MudBlazor;
using SafeCare.Services;
using SafeCare.Tests.Integration.Infrastructure;
using SafeCare.ViewModels;

namespace SafeCare.Tests.Integration;

public class IncidentReportServiceSortingTests(PostgresFixture fixture) : IntegrationTestBase(fixture)
{
    private IncidentReportService CreateSut() => new(DbFactory, EmailQueue);

    private static IncidentReportsRequestVm SortedBy(string column, bool descending) => new()
    {
        Page = 0,
        PageSize = 50,
        SortDefinitions = [new SortDefinition<IncidentReportsGridItem>(column, descending, 0, x => x.Id)]
    };

    [Fact]
    public async Task SortsByReporterSurnameAscending()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, name: "Anna", surname: "Zielińska");
        await SeedReportAsync(department, name: "Piotr", surname: "Adamski");

        var result = await CreateSut().GetReports(
            SortedBy(nameof(IncidentReportsGridItem.FullName), descending: false));

        Assert.Equal("Piotr Adamski", result.Items.First().FullName);
    }

    [Fact]
    public async Task SortsByReporterSurnameDescending()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, name: "Anna", surname: "Zielińska");
        await SeedReportAsync(department, name: "Piotr", surname: "Adamski");

        var result = await CreateSut().GetReports(
            SortedBy(nameof(IncidentReportsGridItem.FullName), descending: true));

        Assert.Equal("Anna Zielińska", result.Items.First().FullName);
    }

    [Fact]
    public async Task SortsByPatientAgeAscendingMeaningYoungestFirst()
    {
        // Age is derived from the date of birth, so the sort direction is deliberately
        // inverted: the youngest patient has the most recent PatientDob.
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, patientName: "Stary", patientDob: new DateTime(1940, 1, 1));
        await SeedReportAsync(department, patientName: "Młody", patientDob: new DateTime(2010, 1, 1));

        var result = await CreateSut().GetReports(
            SortedBy(nameof(IncidentReportsGridItem.PatientAge), descending: false));

        Assert.StartsWith("Młody", result.Items.First().PatientFullName);
    }

    [Fact]
    public async Task SortsByPatientAgeDescendingMeaningOldestFirst()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, patientName: "Stary", patientDob: new DateTime(1940, 1, 1));
        await SeedReportAsync(department, patientName: "Młody", patientDob: new DateTime(2010, 1, 1));

        var result = await CreateSut().GetReports(
            SortedBy(nameof(IncidentReportsGridItem.PatientAge), descending: true));

        Assert.StartsWith("Stary", result.Items.First().PatientFullName);
    }

    [Fact]
    public async Task SortsByDepartmentNameNotByForeignKey()
    {
        // Seeded so that alphabetical order is the opposite of insertion order; ordering by
        // the navigation property would sort by Id and pass by accident otherwise.
        var zebra = await SeedDepartmentAsync("Zakład patomorfologii", "ZP");
        var alpha = await SeedDepartmentAsync("Anestezjologia", "AN");
        await SeedReportAsync(zebra);
        await SeedReportAsync(alpha);

        var result = await CreateSut().GetReports(
            SortedBy(nameof(IncidentReportsGridItem.Department), descending: false));

        Assert.Equal("Anestezjologia", result.Items.First().Department);
    }

    [Fact]
    public async Task SortsByIdDescendingByDefault()
    {
        var department = await SeedDepartmentAsync();
        var first = await SeedReportAsync(department);
        var second = await SeedReportAsync(department);

        var result = await CreateSut().GetReports(new IncidentReportsRequestVm { PageSize = 50 });

        Assert.Equal(second.Id, result.Items.First().Id);
        Assert.Equal(first.Id, result.Items.Last().Id);
    }

    [Fact]
    public async Task ReportsTheTotalCountIndependentlyOfThePageSize()
    {
        var department = await SeedDepartmentAsync();
        for (var i = 0; i < 5; i++)
        {
            await SeedReportAsync(department);
        }

        var result = await CreateSut().GetReports(new IncidentReportsRequestVm { Page = 0, PageSize = 2 });

        Assert.Equal(5, result.ItemTotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task ReturnsTheRequestedPage()
    {
        var department = await SeedDepartmentAsync();
        var reports = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            reports.Add((await SeedReportAsync(department)).Id);
        }

        var request = SortedBy(nameof(IncidentReportsGridItem.Id), descending: false);
        request.Page = 1;
        request.PageSize = 2;

        var result = await CreateSut().GetReports(request);

        Assert.Equal([reports[2], reports[3]], result.Items.Select(i => i.Id));
    }
}
```

- [ ] **Step 2: Uruchom testy i commituj**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj -- --filter-trait "Category=Integration"
```

```bash
git add SafeCare.Tests/Integration/IncidentReportServiceSortingTests.cs
git commit -m "Add report grid sorting and paging coverage"
```

---

### Task 14: Testy szczegółów i zmian stanu zgłoszenia

**Files:**
- Create: `SafeCare.Tests/Integration/IncidentReportServiceDetailsTests.cs`

**Interfaces:**
- Consumes: `IntegrationTestBase` z Task 10
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

```csharp
using Microsoft.EntityFrameworkCore;
using SafeCare.Enums;
using SafeCare.Exceptions;
using SafeCare.Services;
using SafeCare.Tests.Integration.Infrastructure;

namespace SafeCare.Tests.Integration;

public class IncidentReportServiceDetailsTests(PostgresFixture fixture) : IntegrationTestBase(fixture)
{
    private IncidentReportService CreateSut() => new(DbFactory, EmailQueue);

    [Fact]
    public async Task ResolvesDepartmentAndEventTypesForDisplay()
    {
        var department = await SeedDepartmentAsync("Chirurgia", "CH");
        var definition = await SeedDefinitionAsync("Upadek pacjenta", IncidentCategory.Clinical);
        var report = await SeedReportAsync(department, [definition]);

        var details = await CreateSut().GetReportDetails(report.Id);

        Assert.Equal("Chirurgia", details.Department);
        Assert.Equal("Upadek pacjenta", Assert.Single(details.Incidents).Name);
        Assert.Equal("Działalność kliniczna", details.Incidents.Single().Category);
    }

    [Fact]
    public async Task AppendsFreeTextAsAnAdditionalEventUnderTheOtherCategory()
    {
        var department = await SeedDepartmentAsync();
        var definition = await SeedDefinitionAsync();
        var report = await SeedReportAsync(department, [definition], otherIncidentDefinition: "Nietypowe zdarzenie");

        var details = await CreateSut().GetReportDetails(report.Id);

        Assert.Equal(2, details.Incidents.Count);
        Assert.Contains(details.Incidents, i => i.Category == "Inne" && i.Name == "Nietypowe zdarzenie");
    }

    [Fact]
    public async Task TranslatesPatientGenderForDisplay()
    {
        var department = await SeedDepartmentAsync();
        var report = await SeedReportAsync(department, gender: Gender.Female);

        var details = await CreateSut().GetReportDetails(report.Id);

        Assert.Equal("Kobieta", details.PatientGender);
    }

    [Fact]
    public async Task ThrowsWhenTheReportDoesNotExist()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateSut().GetReportDetails(9999));
    }

    [Fact]
    public async Task MovesAReportToANewStatus()
    {
        var department = await SeedDepartmentAsync();
        var report = await SeedReportAsync(department);

        await CreateSut().UpdateStatus(report.Id, ReportStatus.Resolved);

        await using var db = CreateDbContext();
        Assert.Equal(ReportStatus.Resolved, (await db.IncidentReports.SingleAsync(r => r.Id == report.Id)).Status);
    }

    [Fact]
    public async Task ThrowsWhenUpdatingTheStatusOfAMissingReport()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().UpdateStatus(9999, ReportStatus.Resolved));
    }

    [Fact]
    public async Task DeletesAReport()
    {
        var department = await SeedDepartmentAsync();
        var report = await SeedReportAsync(department);

        await CreateSut().DeleteReport(report.Id);

        await using var db = CreateDbContext();
        Assert.False(await db.IncidentReports.AnyAsync(r => r.Id == report.Id));
    }

    [Fact]
    public async Task ThrowsWhenDeletingAMissingReport()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateSut().DeleteReport(9999));
    }
}
```

- [ ] **Step 2: Uruchom testy i commituj**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj -- --filter-trait "Category=Integration"
```

```bash
git add SafeCare.Tests/Integration/IncidentReportServiceDetailsTests.cs
git commit -m "Add report details and status change coverage"
```

---

### Task 15: Testy zarządzania kontami

**Files:**
- Create: `SafeCare.Tests/Integration/UserManagementServiceTests.cs`

**Interfaces:**
- Consumes: `IntegrationTestBase`, `IdentityServiceProvider` z Task 10
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SafeCare.Data.Entities;
using SafeCare.Dtos;
using SafeCare.Services;
using SafeCare.Tests.Integration.Infrastructure;

namespace SafeCare.Tests.Integration;

public class UserManagementServiceTests(PostgresFixture fixture) : IntegrationTestBase(fixture)
{
    private ServiceProvider _provider = null!;

    private UserManagementService CreateSut()
    {
        _provider = IdentityServiceProvider.Build(Fixture.Options);
        var userManager = _provider.GetRequiredService<UserManager<User>>();
        return new UserManagementService(userManager, DbFactory);
    }

    private static CreateUserDto ValidUser(string userName = "nowak") => new()
    {
        UserName = userName,
        FirstName = "Jan",
        LastName = "Nowak",
        Email = $"{userName}@szpital.pl",
        Password = "Haslo123!",
        Role = AppRoles.User
    };

    [Fact]
    public async Task CreatesAnAccountAndAssignsItsRole()
    {
        var sut = CreateSut();

        var (success, error) = await sut.CreateUserAsync(ValidUser());

        Assert.True(success, error);

        var users = await sut.GetUsersAsync();
        var created = Assert.Single(users, u => u.UserName == "nowak");
        Assert.Equal(AppRoles.User, created.Role);
        Assert.Equal("Jan", created.FirstName);
    }

    [Fact]
    public async Task RejectsADuplicateUserName()
    {
        var sut = CreateSut();
        await sut.CreateUserAsync(ValidUser());

        var duplicate = ValidUser();
        duplicate.Email = "inny@szpital.pl";

        var (success, error) = await sut.CreateUserAsync(duplicate);

        Assert.False(success);
        Assert.Equal("Użytkownik o podanej nazwie już istnieje.", error);
    }

    [Fact]
    public async Task RejectsADuplicateEmailAddress()
    {
        var sut = CreateSut();
        await sut.CreateUserAsync(ValidUser());

        var duplicate = ValidUser("inny");
        duplicate.Email = "nowak@szpital.pl";

        var (success, error) = await sut.CreateUserAsync(duplicate);

        Assert.False(success);
        Assert.Equal("Użytkownik o podanym adresie e-mail już istnieje.", error);
    }

    [Fact]
    public async Task RejectsAnUnknownRole()
    {
        var sut = CreateSut();
        var dto = ValidUser();
        dto.Role = "Superadmin";

        var (success, error) = await sut.CreateUserAsync(dto);

        Assert.False(success);
        Assert.Equal("Wybrano nieprawidłową rolę użytkownika.", error);
    }

    [Fact]
    public async Task RejectsAWeakPasswordWithAPolishMessage()
    {
        var sut = CreateSut();
        var dto = ValidUser();
        dto.Password = "abc";

        var (success, error) = await sut.CreateUserAsync(dto);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains("Hasło", error);
    }

    [Theory]
    [InlineData("", "Jan", "Nowak", "a@b.pl", "Nazwa użytkownika jest wymagana.")]
    [InlineData("nowak", "", "Nowak", "a@b.pl", "Imię jest wymagane.")]
    [InlineData("nowak", "Jan", "", "a@b.pl", "Nazwisko jest wymagane.")]
    [InlineData("nowak", "Jan", "Nowak", "", "Adres e-mail jest wymagany.")]
    public async Task RejectsMissingRequiredFields(
        string userName, string firstName, string lastName, string email, string expectedMessage)
    {
        var sut = CreateSut();

        var (success, error) = await sut.CreateUserAsync(new CreateUserDto
        {
            UserName = userName,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Password = "Haslo123!",
            Role = AppRoles.User
        });

        Assert.False(success);
        Assert.Equal(expectedMessage, error);
    }

    [Fact]
    public async Task RefusesToDeleteTheCallersOwnAccount()
    {
        var sut = CreateSut();
        var someoneElse = Guid.NewGuid();

        var (success, error) = await sut.DeleteUserAsync(someoneElse, someoneElse);

        Assert.False(success);
        Assert.Equal("Nie możesz usunąć własnego konta.", error);
    }

    [Fact]
    public async Task RefusesToDeleteTheLastAdministrator()
    {
        // Only the seeded admin holds the Admin role at this point; removing it would leave
        // the system unmanageable.
        var sut = CreateSut();

        var (success, error) = await sut.DeleteUserAsync(PostgresFixture.SeededAdminId, Guid.NewGuid());

        Assert.False(success);
        Assert.Equal("Nie można usunąć ostatniego administratora.", error);
    }

    [Fact]
    public async Task DeletesAnAdministratorWhenAnotherOneRemains()
    {
        var sut = CreateSut();
        var second = ValidUser("drugiadmin");
        second.Role = AppRoles.Admin;
        await sut.CreateUserAsync(second);

        var users = await sut.GetUsersAsync();
        var target = users.Single(u => u.UserName == "drugiadmin");

        var (success, error) = await sut.DeleteUserAsync(target.Id, PostgresFixture.SeededAdminId);

        Assert.True(success, error);
        Assert.DoesNotContain(await sut.GetUsersAsync(), u => u.UserName == "drugiadmin");
    }

    [Fact]
    public async Task ReportsAMissingAccount()
    {
        var sut = CreateSut();

        var (success, error) = await sut.DeleteUserAsync(Guid.NewGuid(), PostgresFixture.SeededAdminId);

        Assert.False(success);
        Assert.Equal("Nie znaleziono użytkownika.", error);
    }

    [Fact]
    public async Task ListsAccountsOrderedByUserName()
    {
        var sut = CreateSut();
        await sut.CreateUserAsync(ValidUser("zielinski"));
        await sut.CreateUserAsync(ValidUser("adamski"));

        var users = await sut.GetUsersAsync();

        Assert.Equal(users.Select(u => u.UserName).OrderBy(n => n), users.Select(u => u.UserName));
    }
}
```

- [ ] **Step 2: Uruchom testy i commituj**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj -- --filter-trait "Category=Integration"
```

```bash
git add SafeCare.Tests/Integration/UserManagementServiceTests.cs
git commit -m "Add user management service coverage"
```

---

### Task 16: Testy seederów

**Files:**
- Create: `SafeCare.Tests/Integration/SeederTests.cs`

**Interfaces:**
- Consumes: `IntegrationTestBase`, `IdentityServiceProvider` z Task 10
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SafeCare.Data;
using SafeCare.Data.Entities;
using SafeCare.Tests.Integration.Infrastructure;

namespace SafeCare.Tests.Integration;

public class SeederTests(PostgresFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task LoadsDictionaryDataIntoAnEmptyDatabase()
    {
        await using var db = CreateDbContext();

        await DbSeeder.SeedAsync(db);

        Assert.True(await db.Departments.AnyAsync());
        Assert.True(await db.IncidentDefinitions.AnyAsync());
    }

    [Fact]
    public async Task DoesNotDuplicateDataOnASecondRun()
    {
        await using var db = CreateDbContext();
        await DbSeeder.SeedAsync(db);
        var departmentCount = await db.Departments.CountAsync();

        await DbSeeder.SeedAsync(db);

        Assert.Equal(departmentCount, await db.Departments.CountAsync());
    }

    [Fact]
    public async Task LeavesAnAlreadyPopulatedDatabaseAlone()
    {
        await SeedDepartmentAsync("Jedyny oddział", "JO");

        await using var db = CreateDbContext();
        await DbSeeder.SeedAsync(db);

        Assert.Equal(1, await db.Departments.CountAsync());
    }

    [Fact]
    public async Task CreatesRolesAndGivesTheSeededAccountItsAdminRole()
    {
        var provider = IdentityServiceProvider.Build(Fixture.Options);
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        await IdentitySeeder.SeedRolesAndAdminAsync(roleManager, userManager);

        Assert.True(await roleManager.RoleExistsAsync(AppRoles.Admin));
        Assert.True(await roleManager.RoleExistsAsync(AppRoles.User));

        var admin = await userManager.FindByNameAsync("admin");
        Assert.NotNull(admin);
        Assert.True(await userManager.IsInRoleAsync(admin!, AppRoles.Admin));
    }

    [Fact]
    public async Task IsSafeToRunRepeatedly()
    {
        var provider = IdentityServiceProvider.Build(Fixture.Options);
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        await IdentitySeeder.SeedRolesAndAdminAsync(roleManager, userManager);
        var exception = await Record.ExceptionAsync(() =>
            IdentitySeeder.SeedRolesAndAdminAsync(roleManager, userManager));

        Assert.Null(exception);
    }
}
```

- [ ] **Step 2: Uruchom testy i commituj**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj -- --filter-trait "Category=Integration"
```

```bash
git add SafeCare.Tests/Integration/SeederTests.cs
git commit -m "Add database and identity seeder coverage"
```

---

### Task 17: Szkielet E2E + uruchamianie aplikacji + pierwszy scenariusz

**Files:**
- Create: `SafeCare.E2ETests/SafeCare.E2ETests.csproj`
- Create: `SafeCare.E2ETests/Infrastructure/AppFixture.cs`
- Create: `SafeCare.E2ETests/Infrastructure/E2ETestBase.cs`
- Create: `SafeCare.E2ETests/PublicFormLoadTests.cs`
- Modify: `SafeCare.slnx`

**Interfaces:**
- Consumes: nic z poprzednich zadań (osobny projekt)
- Produces:
  - `AppFixture` z `string BaseUrl`, `IBrowser Browser`, `DbContextOptions<AppDbContext> DbOptions`, `string MailPitApiUrl`
  - `E2ETestBase` z `Fixture`, `NewPageAsync()`, `LoginAsAdminAsync(IPage)`

- [ ] **Step 1: Utwórz projekt**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="4.0.0" />
    <PackageReference Include="Microsoft.Playwright" Version="1.62.0" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.14.0" />
    <PackageReference Include="Testcontainers" Version="4.14.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SafeCare\SafeCare.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Dopisz projekt do `SafeCare.slnx`**

```xml
<Solution>
  <Project Path="SafeCare/SafeCare.csproj" />
  <Project Path="SafeCare.Tests/SafeCare.Tests.csproj" />
  <Project Path="SafeCare.E2ETests/SafeCare.E2ETests.csproj" />
</Solution>
```

- [ ] **Step 3: Napisz `AppFixture`**

```csharp
using System.Diagnostics;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using SafeCare.Data;
using Testcontainers.PostgreSql;

namespace SafeCare.E2ETests.Infrastructure;

/// <summary>
/// Brings up everything a browser test needs: a database, a mail server, the application
/// itself and a browser. One instance serves the whole test session.
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private readonly IContainer _mailpit = new ContainerBuilder()
        .WithImage("axllent/mailpit:latest")
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8025))
        .Build();

    private Process? _app;
    private IPlaywright? _playwright;

    public string BaseUrl { get; private set; } = "";

    public string MailPitApiUrl { get; private set; } = "";

    public IBrowser Browser { get; private set; } = null!;

    public DbContextOptions<AppDbContext> DbOptions { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_database.StartAsync(), _mailpit.StartAsync());

        DbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.GetConnectionString())
            .Options;

        MailPitApiUrl = $"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}";

        var port = FreeTcpPort();
        BaseUrl = $"http://127.0.0.1:{port}";

        StartApplication(port);
        await WaitUntilReadyAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_app is { HasExited: false })
        {
            _app.Kill(entireProcessTree: true);
            _app.WaitForExit(10_000);
        }

        _app?.Dispose();

        await _mailpit.DisposeAsync();
        await _database.DisposeAsync();
    }

    private static int FreeTcpPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private void StartApplication(int port)
    {
        var projectPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SafeCare", "SafeCare.csproj"));

        var startInfo = new ProcessStartInfo("dotnet")
        {
            Arguments = $"run --project \"{projectPath}\" --no-build -c Release",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        // Environment variables win over appsettings files. "Testing" additionally stops
        // appsettings.Development.json from loading at all, so a developer's own database
        // can never be reached from here.
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        startInfo.Environment["ConnectionStrings__DefaultConnection"] = _database.GetConnectionString();
        startInfo.Environment["Email__Provider"] = "Smtp";
        startInfo.Environment["Email__Smtp__Host"] = _mailpit.Hostname;
        startInfo.Environment["Email__Smtp__Port"] = _mailpit.GetMappedPublicPort(1025).ToString();
        startInfo.Environment["Email__Smtp__UseSsl"] = "false";
        startInfo.Environment["Email__Smtp__AuthMode"] = "None";

        _app = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the SafeCare process");
    }

    /// <summary>
    /// Polls the home page until the application answers. Startup includes applying
    /// migrations and seeding, so the first response can take a while.
    /// </summary>
    private async Task WaitUntilReadyAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMinutes(2);

        while (DateTime.UtcNow < deadline)
        {
            if (_app is { HasExited: true })
            {
                var output = await _app.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"SafeCare exited during startup: {output}");
            }

            try
            {
                var response = await client.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // not listening yet
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"SafeCare did not become ready at {BaseUrl}");
    }
}

[CollectionDefinition(AppCollection.Name)]
public sealed class AppCollection : ICollectionFixture<AppFixture>
{
    public const string Name = "app";
}
```

- [ ] **Step 4: Napisz `E2ETestBase`**

```csharp
using Microsoft.Playwright;

namespace SafeCare.E2ETests.Infrastructure;

[Collection(AppCollection.Name)]
public abstract class E2ETestBase(AppFixture fixture)
{
    /// <summary>
    /// The public form refuses anything submitted sooner than this after loading — it is a
    /// bot defence, not a delay to optimise away. Tests must wait it out.
    /// </summary>
    protected static readonly TimeSpan MinimumFormFillTime = TimeSpan.FromSeconds(6);

    protected AppFixture Fixture { get; } = fixture;

    protected async Task<IPage> NewPageAsync()
    {
        var context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Fixture.BaseUrl,
            IgnoreHTTPSErrors = true
        });

        return await context.NewPageAsync();
    }

    protected async Task LoginAsAdminAsync(IPage page)
    {
        await page.GotoAsync("/login");
        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "Admin123!");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForURLAsync(url => !url.Contains("/login"));
    }
}
```

Selektory logowania odpowiadają polom formularza w `Login.razor`, który jest zwykłym `<form method="post" action="/signin">`. Otwórz ten plik i dopasuj atrybuty `name`, jeśli różnią się od `username`/`password`.

- [ ] **Step 5: Napisz pierwszy scenariusz**

```csharp
using SafeCare.E2ETests.Infrastructure;

namespace SafeCare.E2ETests;

public class PublicFormLoadTests(AppFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task ServesThePublicFormAnonymously()
    {
        var page = await NewPageAsync();

        var response = await page.GotoAsync("/");

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Contains("SafeCare", await page.TitleAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RendersTheFormWithoutClientSideErrors()
    {
        var page = await NewPageAsync();
        var consoleErrors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                consoleErrors.Add(message.Text);
            }
        };

        await page.GotoAsync("/");
        await page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        Assert.Empty(consoleErrors);
    }
}
```

- [ ] **Step 6: Zbuduj i zainstaluj przeglądarki**

Instalacja przeglądarek korzysta ze skryptu generowanego do katalogu wyjściowego przy budowaniu.

```bash
dotnet build SafeCare.E2ETests/SafeCare.E2ETests.csproj -c Release
```

```bash
pwsh SafeCare.E2ETests/bin/Release/net10.0/playwright.ps1 install chromium
```

- [ ] **Step 7: Zbuduj aplikację w Release i uruchom testy**

`AppFixture` startuje aplikację z `--no-build`, więc musi ona być wcześniej zbudowana.

```bash
dotnet build SafeCare/SafeCare.csproj -c Release
```

```bash
dotnet test SafeCare.E2ETests/SafeCare.E2ETests.csproj
```

Jeśli start aplikacji się nie powiedzie, komunikat wyjątku zawiera jej wyjście diagnostyczne.

- [ ] **Step 8: Commit**

```bash
git add SafeCare.slnx SafeCare.E2ETests
git commit -m "Add E2E project with application fixture"
```

---

### Task 18: E2E — wysłanie zgłoszenia przez formularz publiczny

**Files:**
- Create: `SafeCare.E2ETests/Infrastructure/PublicFormPage.cs`
- Create: `SafeCare.E2ETests/PublicFormSubmissionTests.cs`

**Interfaces:**
- Consumes: `E2ETestBase`, `AppFixture` z Task 17
- Produces: `PublicFormPage` z `FillMinimalAsync(...)`, `SubmitAsync()`

- [ ] **Step 1: Napisz obiekt strony**

Selektory opieramy o etykiety i role, nie o klasy MudBlazora, które zmieniają się między wersjami. Otwórz `SafeCare/Components/Pages/Home.razor` i dopasuj teksty etykiet do rzeczywistych — poniższe odpowiadają polom opisanym w formularzu.

```csharp
using Microsoft.Playwright;

namespace SafeCare.E2ETests.Infrastructure;

/// <summary>
/// Drives the public incident form. Selectors go through labels and roles so that a MudBlazor
/// upgrade changing generated class names does not break every test.
/// </summary>
public sealed class PublicFormPage(IPage page)
{
    public IPage Page { get; } = page;

    public async Task OpenAsync() => await Page.GotoAsync("/");

    /// <summary>
    /// Fills only what the validator requires, leaving reporter and patient details empty.
    /// </summary>
    public async Task FillMinimalAsync(string description, string department, string incidentType)
    {
        await Page.GetByLabel("Miejsce zdarzenia").ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = department }).ClickAsync();

        await Page.GetByRole(AriaRole.Checkbox, new() { Name = incidentType }).CheckAsync();

        await Page.GetByLabel("Data").FillAsync(DateTime.Today.AddDays(-1).ToString("dd.MM.yyyy"));
        await Page.GetByLabel("Czas").FillAsync("10:30");

        await Page.GetByLabel("Opis zdarzenia").FillAsync(description);
    }

    public async Task FillReporterAsync(string name, string surname)
    {
        await Page.GetByLabel("Imię", new() { Exact = true }).FillAsync(name);
        await Page.GetByLabel("Nazwisko", new() { Exact = true }).FillAsync(surname);
    }

    public async Task SubmitAsync() =>
        await Page.GetByRole(AriaRole.Button, new() { Name = "Wyślij" }).ClickAsync();
}
```

- [ ] **Step 2: Napisz testy wysyłki**

```csharp
using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.E2ETests.Infrastructure;

namespace SafeCare.E2ETests;

public class PublicFormSubmissionTests(AppFixture fixture) : E2ETestBase(fixture)
{
    private async Task<string?> LatestDescriptionAsync()
    {
        await using var db = new AppDbContext(Fixture.DbOptions);
        return await db.IncidentReports
            .OrderByDescending(r => r.Id)
            .Select(r => r.IncidentDescription)
            .FirstOrDefaultAsync();
    }

    [Fact]
    public async Task StoresAnAnonymousReportSubmittedThroughTheForm()
    {
        var description = $"Zgłoszenie testowe {Guid.NewGuid()}";
        var form = new PublicFormPage(await NewPageAsync());

        await form.OpenAsync();
        await form.FillMinimalAsync(description, "Chirurgia", "Upadek pacjenta");

        // The bot defence rejects anything submitted within five seconds of load.
        await Task.Delay(MinimumFormFillTime);
        await form.SubmitAsync();

        await form.Page.WaitForTimeoutAsync(2000);

        Assert.Equal(description, await LatestDescriptionAsync());
    }

    [Fact]
    public async Task StoresAReportCarryingReporterDetails()
    {
        var description = $"Zgłoszenie imienne {Guid.NewGuid()}";
        var form = new PublicFormPage(await NewPageAsync());

        await form.OpenAsync();
        await form.FillMinimalAsync(description, "Chirurgia", "Upadek pacjenta");
        await form.FillReporterAsync("Paweł", "Wiśniewski");

        await Task.Delay(MinimumFormFillTime);
        await form.SubmitAsync();
        await form.Page.WaitForTimeoutAsync(2000);

        await using var db = new AppDbContext(Fixture.DbOptions);
        var stored = await db.IncidentReports
            .OrderByDescending(r => r.Id)
            .FirstAsync(r => r.IncidentDescription == description);

        Assert.Equal("Paweł", stored.Name);
        Assert.Equal("Wiśniewski", stored.Surname);
    }
}
```

Wartości `"Chirurgia"` i `"Upadek pacjenta"` muszą istnieć w `SafeCare/Data/SeedData.sql`, bo aplikacja zasiewa się sama przy starcie. Sprawdź ten plik i podstaw rzeczywiste nazwy oddziału i rodzaju zdarzenia.

- [ ] **Step 3: Uruchom testy i commituj**

```bash
dotnet test SafeCare.E2ETests/SafeCare.E2ETests.csproj
```

```bash
git add SafeCare.E2ETests/Infrastructure/PublicFormPage.cs SafeCare.E2ETests/PublicFormSubmissionTests.cs
git commit -m "Add E2E coverage for public form submission"
```

---

### Task 19: E2E — walidacja i obrona przed botami

**Files:**
- Create: `SafeCare.E2ETests/PublicFormValidationTests.cs`

**Interfaces:**
- Consumes: `PublicFormPage`, `E2ETestBase` z Task 17 i 18
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

```csharp
using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.E2ETests.Infrastructure;

namespace SafeCare.E2ETests;

public class PublicFormValidationTests(AppFixture fixture) : E2ETestBase(fixture)
{
    private async Task<int> ReportCountAsync()
    {
        await using var db = new AppDbContext(Fixture.DbOptions);
        return await db.IncidentReports.CountAsync();
    }

    [Fact]
    public async Task ShowsPolishValidationMessagesForAnEmptyForm()
    {
        var form = new PublicFormPage(await NewPageAsync());
        await form.OpenAsync();

        await Task.Delay(MinimumFormFillTime);
        await form.SubmitAsync();

        await form.Page.WaitForTimeoutAsync(1000);
        var content = await form.Page.ContentAsync();

        Assert.Contains("Opis zdarzenia jest wymagany", content);
    }

    [Fact]
    public async Task SilentlyDiscardsASubmissionSentTooQuickly()
    {
        // The bot defence deliberately gives no feedback — a bot must not learn why it failed.
        // The report simply must not exist.
        var description = $"Zbyt szybkie zgłoszenie {Guid.NewGuid()}";
        var before = await ReportCountAsync();

        var form = new PublicFormPage(await NewPageAsync());
        await form.OpenAsync();
        await form.FillMinimalAsync(description, "Chirurgia", "Upadek pacjenta");
        await form.SubmitAsync();

        await form.Page.WaitForTimeoutAsync(2000);

        Assert.Equal(before, await ReportCountAsync());

        await using var db = new AppDbContext(Fixture.DbOptions);
        Assert.False(await db.IncidentReports.AnyAsync(r => r.IncidentDescription == description));
    }
}
```

- [ ] **Step 2: Uruchom testy i commituj**

```bash
dotnet test SafeCare.E2ETests/SafeCare.E2ETests.csproj
```

```bash
git add SafeCare.E2ETests/PublicFormValidationTests.cs
git commit -m "Add E2E coverage for form validation and bot defence"
```

---

### Task 20: E2E — powiadomienie e-mail przez MailPit

Jedyne miejsce, w którym `EmailBackgroundService` i realna wysyłka SMTP są w ogóle wykonywane.

**Files:**
- Create: `SafeCare.E2ETests/Infrastructure/MailPitClient.cs`
- Create: `SafeCare.E2ETests/EmailNotificationTests.cs`

**Interfaces:**
- Consumes: `AppFixture.MailPitApiUrl` z Task 17
- Produces: `MailPitClient` z `DeleteAllAsync()`, `WaitForMessageAsync(string subjectFragment, TimeSpan timeout)`

- [ ] **Step 1: Napisz klienta MailPit**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SafeCare.E2ETests.Infrastructure;

public sealed record MailPitMessage(
    [property: JsonPropertyName("ID")] string Id,
    [property: JsonPropertyName("Subject")] string Subject,
    [property: JsonPropertyName("Bcc")] List<MailPitAddress>? Bcc,
    [property: JsonPropertyName("To")] List<MailPitAddress>? To);

public sealed record MailPitAddress([property: JsonPropertyName("Address")] string Address);

/// <summary>
/// Reads what the application actually handed to the mail server.
/// </summary>
public sealed class MailPitClient(string baseUrl) : IDisposable
{
    private readonly HttpClient _client = new() { BaseAddress = new Uri(baseUrl) };

    public async Task DeleteAllAsync() => await _client.DeleteAsync("/api/v1/messages");

    /// <summary>
    /// Polls until a message whose subject contains <paramref name="subjectFragment"/> arrives.
    /// Delivery is asynchronous — the background service drains a queue — so waiting is required.
    /// </summary>
    public async Task<MailPitMessage> WaitForMessageAsync(string subjectFragment, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var payload = await _client.GetStringAsync("/api/v1/messages");
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.TryGetProperty("messages", out var messages))
            {
                foreach (var element in messages.EnumerateArray())
                {
                    var message = element.Deserialize<MailPitMessage>();
                    if (message is not null && message.Subject.Contains(subjectFragment))
                    {
                        return message;
                    }
                }
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"No message with subject containing '{subjectFragment}' arrived");
    }

    public void Dispose() => _client.Dispose();
}
```

- [ ] **Step 2: Napisz test powiadomienia**

```csharp
using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.E2ETests.Infrastructure;

namespace SafeCare.E2ETests;

public class EmailNotificationTests(AppFixture fixture) : E2ETestBase(fixture)
{
    /// <summary>Opts the seeded administrator into notifications so there is a recipient.</summary>
    private async Task EnableNotificationsForAdminAsync()
    {
        await using var db = new AppDbContext(Fixture.DbOptions);
        var admin = await db.Users.FirstAsync(u => u.UserName == "admin");
        admin.ReceiveEmailNotifications = true;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task DeliversANotificationWithRecipientsHiddenInBcc()
    {
        await EnableNotificationsForAdminAsync();

        using var mail = new MailPitClient(Fixture.MailPitApiUrl);
        await mail.DeleteAllAsync();

        var description = $"Zgłoszenie z powiadomieniem {Guid.NewGuid()}";
        var form = new PublicFormPage(await NewPageAsync());
        await form.OpenAsync();
        await form.FillMinimalAsync(description, "Chirurgia", "Upadek pacjenta");
        await Task.Delay(MinimumFormFillTime);
        await form.SubmitAsync();

        var message = await mail.WaitForMessageAsync("[SafeCare] Nowe zgłoszenie", TimeSpan.FromSeconds(60));

        Assert.Contains("Nowe zgłoszenie zdarzenia", message.Subject);

        // Staff addresses must never appear in a header other recipients can read.
        Assert.NotNull(message.Bcc);
        Assert.Contains(message.Bcc!, a => a.Address == "system@admin.pl");
        Assert.DoesNotContain(message.To ?? [], a => a.Address == "system@admin.pl");
    }
}
```

- [ ] **Step 3: Uruchom testy i commituj**

```bash
dotnet test SafeCare.E2ETests/SafeCare.E2ETests.csproj
```

Jeśli test przekroczy limit czasu, sprawdź wyjście aplikacji — najczęstsza przyczyna to niedopasowany port SMTP w zmiennych środowiskowych `AppFixture`.

```bash
git add SafeCare.E2ETests/Infrastructure/MailPitClient.cs SafeCare.E2ETests/EmailNotificationTests.cs
git commit -m "Add E2E coverage for email notification delivery"
```

---

### Task 21: E2E — logowanie i autoryzacja

Test `returnUrl` celuje w udokumentowaną ostrą krawędź, która gubi przekierowanie po cichu.

**Files:**
- Create: `SafeCare.E2ETests/AuthenticationTests.cs`

**Interfaces:**
- Consumes: `E2ETestBase.LoginAsAdminAsync` z Task 17
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

```csharp
using SafeCare.E2ETests.Infrastructure;

namespace SafeCare.E2ETests;

public class AuthenticationTests(AppFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task SignsInWithValidCredentialsAndLandsOnTheDashboard()
    {
        var page = await NewPageAsync();

        await LoginAsAdminAsync(page);

        Assert.Contains("/dashboard", page.Url);
    }

    [Fact]
    public async Task RejectsInvalidCredentialsAndReturnsToLoginWithAnError()
    {
        var page = await NewPageAsync();

        await page.GotoAsync("/login");
        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "ZleHaslo123!");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForLoadStateAsync();

        Assert.Contains("/login", page.Url);
        Assert.Contains("error", page.Url);
    }

    [Fact]
    public async Task SendsAnAnonymousVisitorToLoginAndBackToTheRequestedPage()
    {
        // The returnUrl round trip has a sharp edge: /signin only accepts paths beginning with
        // "/", while ToBaseRelativePath yields none. A regression there drops the redirect
        // silently and the user lands on the dashboard instead.
        var page = await NewPageAsync();

        await page.GotoAsync("/account");
        await page.WaitForURLAsync(url => url.Contains("/login"));

        Assert.Contains("returnUrl", page.Url);

        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "Admin123!");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForURLAsync(url => !url.Contains("/login"));

        Assert.Contains("/account", page.Url);
    }

    [Fact]
    public async Task BlocksAnonymousAccessToTheDashboard()
    {
        var page = await NewPageAsync();

        await page.GotoAsync("/dashboard");
        await page.WaitForURLAsync(url => url.Contains("/login"));

        Assert.Contains("/login", page.Url);
    }

    [Fact]
    public async Task EndsTheSessionOnSignOut()
    {
        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);

        await page.GotoAsync("/signout");
        await page.WaitForLoadStateAsync();

        await page.GotoAsync("/dashboard");
        await page.WaitForURLAsync(url => url.Contains("/login"));

        Assert.Contains("/login", page.Url);
    }
}
```

Wylogowanie jest formularzowym POST-em do `/signout`. Jeśli nawigacja GET-em nie zadziała, znajdź przycisk wylogowania w `MainLayout.razor` i kliknij go zamiast tego.

- [ ] **Step 2: Uruchom testy i commituj**

```bash
dotnet test SafeCare.E2ETests/SafeCare.E2ETests.csproj
```

```bash
git add SafeCare.E2ETests/AuthenticationTests.cs
git commit -m "Add E2E coverage for authentication and authorization redirects"
```

---

### Task 22: E2E — dashboard i szczegóły zgłoszenia

**Files:**
- Create: `SafeCare.E2ETests/DashboardTests.cs`

**Interfaces:**
- Consumes: `E2ETestBase` z Task 17
- Produces: nic dla kolejnych zadań

- [ ] **Step 1: Napisz testy**

Aplikacja zasiewa ~200 demonstracyjnych zgłoszeń przy starcie, więc dashboard ma na czym pracować.

```csharp
using Microsoft.Playwright;
using SafeCare.E2ETests.Infrastructure;

namespace SafeCare.E2ETests;

public class DashboardTests(AppFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task ShowsTheReportGridToASignedInUser()
    {
        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);

        await page.GotoAsync("/dashboard");
        await page.WaitForSelectorAsync("table");

        var rowCount = await page.Locator("table tbody tr").CountAsync();

        Assert.True(rowCount > 0, "The seeded demo reports should populate the grid");
    }

    [Fact]
    public async Task KeepsFilterStateInTheUrlSoTheViewIsBookmarkable()
    {
        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);

        // The status filter is round-tripped through the query string by design.
        await page.GotoAsync("/dashboard?statuses=New");
        await page.WaitForSelectorAsync("table");

        var filteredRows = await page.Locator("table tbody tr").CountAsync();

        // Reopening the same address must reproduce the same view.
        var second = await NewPageAsync();
        await LoginAsAdminAsync(second);
        await second.GotoAsync("/dashboard?statuses=New");
        await second.WaitForSelectorAsync("table");

        Assert.Equal(filteredRows, await second.Locator("table tbody tr").CountAsync());
    }

    [Fact]
    public async Task OpensAReportFromTheGrid()
    {
        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);

        await page.GotoAsync("/dashboard");
        await page.WaitForSelectorAsync("table tbody tr");
        await page.Locator("table tbody tr").First.ClickAsync();

        await page.WaitForURLAsync(url => url.Contains("/details/"));

        Assert.Contains("/details/", page.Url);
    }
}
```

Parametr zapytania `statuses` i sposób otwierania szczegółów muszą odpowiadać implementacji w `Dashboard.razor` — otwórz ten plik i dopasuj nazwę parametru oraz interakcję otwierającą wiersz (może to być przycisk w kolumnie akcji zamiast kliknięcia w wiersz).

- [ ] **Step 2: Uruchom testy i commituj**

```bash
dotnet test SafeCare.E2ETests/SafeCare.E2ETests.csproj
```

```bash
git add SafeCare.E2ETests/DashboardTests.cs
git commit -m "Add E2E coverage for dashboard and report details"
```

---

### Task 23: Pipeline CI — job build-test

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: projekty testowe ze wszystkich poprzednich zadań
- Produces: job `build-test`, do którego Task 24 dopisze `e2e`

- [ ] **Step 1: Sprawdź lokalnie, co pokaże kontrola formatowania**

To krok rozpoznawczy — uruchom go **przed** napisaniem workflow.

```bash
dotnet format SafeCare.slnx --verify-no-changes --verbosity diagnostic
```

Jeśli wynik jest czysty, w kroku 3 zostaw bramkę formatowania włączoną. Jeśli pokazuje wiele plików, **nie przeformatowuj drzewa** — zgłoś skalę właścicielowi repozytorium i tymczasowo zawęź krok do `--severity error`.

- [ ] **Step 2: Sprawdź lokalnie skan podatności**

```bash
dotnet list SafeCare.slnx package --vulnerable --include-transitive
```

Zanotuj, czy wynik jest czysty. Komenda zwraca kod 0 nawet przy znaleziskach, więc krok w CI musi analizować tekst.

- [ ] **Step 3: Napisz workflow**

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-test:
    name: Build and test
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore SafeCare.slnx

      - name: Build
        run: dotnet build SafeCare.slnx -c Release --no-restore

      - name: Unit and integration tests
        # The runner provides Docker, so Testcontainers starts PostgreSQL unaided.
        run: dotnet test SafeCare.Tests/SafeCare.Tests.csproj -c Release --no-build

      - name: Vulnerable package scan
        # The command reports findings but always exits 0, so the output has to be inspected.
        run: |
          output=$(dotnet list SafeCare.slnx package --vulnerable --include-transitive)
          echo "$output"
          if echo "$output" | grep -q "has the following vulnerable packages"; then
            echo "::error::Vulnerable packages detected"
            exit 1
          fi

      - name: Check for pending EF model changes
        run: |
          # AppDbContextFactory reads appsettings.Development.json, which is gitignored.
          # has-pending-model-changes needs no live database, only a factory that constructs.
          cat > SafeCare/appsettings.Development.json <<'JSON'
          {
            "ConnectionStrings": {
              "DefaultConnection": "Host=localhost;Port=5432;Database=SafeCare;Username=postgres;Password=postgres"
            }
          }
          JSON
          dotnet tool install --global dotnet-ef
          dotnet ef migrations has-pending-model-changes --project SafeCare --no-build -c Release

      - name: Format check
        run: dotnet format SafeCare.slnx --verify-no-changes
```

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "Add CI workflow for build, test and code health checks"
```

---

### Task 24: Pipeline CI — job E2E

**Files:**
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: job `build-test` z Task 23
- Produces: kompletny pipeline

- [ ] **Step 1: Dopisz job `e2e` na końcu pliku**

```yaml
  e2e:
    name: End-to-end tests
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build
        # AppFixture starts the application with --no-build, so it must exist beforehand.
        run: dotnet build SafeCare.slnx -c Release

      - name: Install Playwright browsers
        run: pwsh SafeCare.E2ETests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium

      - name: Run E2E tests
        run: dotnet test SafeCare.E2ETests/SafeCare.E2ETests.csproj -c Release --no-build

      - name: Upload Playwright traces
        # Without these a browser failure on someone else's machine is guesswork.
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-traces
          path: SafeCare.E2ETests/bin/Release/net10.0/playwright-traces
          if-no-files-found: ignore
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "Add E2E job to CI workflow"
```

---

### Task 25: Aktualizacja dokumentacji

Trzy pliki twierdzą dziś, że projekt nie ma testów ani CI. Po wykonaniu poprzednich zadań to nieprawda.

**Files:**
- Modify: `CLAUDE.md`
- Modify: `AGENTS.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: wszystkie poprzednie zadania
- Produces: nic

- [ ] **Step 1: Popraw `CLAUDE.md`**

Znajdź sekcję zaczynającą się od `**There is no test project and no CI.**` i zastąp ją opisem rzeczywistości:

```markdown
## Testing

Two test projects, both xUnit v3 on Microsoft.Testing.Platform. `global.json` opts into that
runner — without it `dotnet test` fails outright. Test projects are executables
(`<OutputType>Exe</OutputType>`) and must NOT reference `Microsoft.NET.Test.Sdk` or
`xunit.runner.visualstudio`; on .NET 10 those route to VSTest, which is no longer supported.

- `SafeCare.Tests` — unit tests plus integration tests against a real PostgreSQL started by
  Testcontainers. Integration tests carry `[Trait("Category", "Integration")]`.
- `SafeCare.E2ETests` — Playwright driving the real application, with PostgreSQL and MailPit
  in containers.

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

Fast suite only, no Docker needed:

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj -- --filter-not-trait "Category=Integration"
```

E2E needs the app built in Release and browsers installed once
(`pwsh SafeCare.E2ETests/bin/Release/net10.0/playwright.ps1 install chromium`).

Integration tests use a real PostgreSQL because `GetReports` filters through
`EF.Functions.ILike`, which no in-memory provider can translate.

CI runs on GitHub Actions ([ci.yml](.github/workflows/ci.yml)): build and test in one job,
E2E in another, plus vulnerable-package scanning, EF migration drift detection and formatting.
```

- [ ] **Step 2: Popraw `AGENTS.md`**

Odszukaj wzmianki o braku testów i CI, zastąp je skróconą wersją powyższego. Dopisz oba projekty testowe do tabeli „where to look".

- [ ] **Step 3: Popraw `README.md`**

Znajdź zdanie mówiące, że projekt nie ma testów, i wstaw w to miejsce sekcję:

```markdown
## Testy

Wymagania wstępne: uruchomiony Docker (testy integracyjne i E2E startują kontenery
PostgreSQL i MailPit) oraz jednorazowa instalacja przeglądarek Playwrighta.

Cały szybki zestaw — testy jednostkowe i integracyjne:

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

Same testy jednostkowe, bez Dockera:

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj -- --filter-not-trait "Category=Integration"
```

Testy E2E wymagają aplikacji zbudowanej w konfiguracji Release:

```bash
dotnet build SafeCare.slnx -c Release
```

```bash
pwsh SafeCare.E2ETests/bin/Release/net10.0/playwright.ps1 install chromium
```

```bash
dotnet test SafeCare.E2ETests/SafeCare.E2ETests.csproj
```

Każdy push i pull request do `main` uruchamia oba zestawy w GitHub Actions.
```

- [ ] **Step 4: Sprawdź, że wszystko przechodzi w komplecie**

```bash
dotnet test SafeCare.Tests/SafeCare.Tests.csproj
```

```bash
dotnet test SafeCare.E2ETests/SafeCare.E2ETests.csproj
```

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md AGENTS.md README.md
git commit -m "Document the test suites and CI pipeline"
```

---

## Uwagi dla wykonawcy

**Selektory E2E są przybliżeniem.** Obiekty stron w zadaniach 17–22 zostały napisane na podstawie opisu formularza, a nie zrzutu DOM. Przy pierwszym uruchomieniu każdego z tych zadań otwórz odpowiedni plik `.razor` i dopasuj etykiety, nazwy pól i parametry zapytania do rzeczywistości. To oczekiwana część pracy, nie usterka planu.

**Nazwy z danych zasiewanych.** Testy E2E odwołują się do oddziału „Chirurgia" i rodzaju zdarzenia „Upadek pacjenta". Zweryfikuj je w `SafeCare/Data/SeedData.sql` i podstaw rzeczywiste.

**Gdy test padnie, najpierw ustal, czy nie znalazł prawdziwego błędu.** Kilka testów w tym planie celuje w defekty, które w tym projekcie już wystąpiły — odwrócone sortowanie, filtr kategorii „Inne", kodowanie plików. Zaświecenie się któregoś z nich jest sygnałem do zbadania kodu produkcyjnego, a nie do osłabienia asercji.
