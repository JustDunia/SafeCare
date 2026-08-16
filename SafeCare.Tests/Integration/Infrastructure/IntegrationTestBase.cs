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
