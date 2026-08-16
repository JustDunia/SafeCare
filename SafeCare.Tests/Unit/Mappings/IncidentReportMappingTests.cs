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
