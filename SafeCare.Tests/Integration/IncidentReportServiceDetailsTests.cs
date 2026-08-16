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
