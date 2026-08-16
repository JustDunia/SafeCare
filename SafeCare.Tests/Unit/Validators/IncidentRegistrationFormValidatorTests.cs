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
