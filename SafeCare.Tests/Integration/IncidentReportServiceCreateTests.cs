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
