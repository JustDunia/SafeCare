using MudBlazor;
using SafeCare.Enums;
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
    public async Task SortsByDepartmentNameDescending()
    {
        var zebra = await SeedDepartmentAsync("Zakład patomorfologii", "ZP");
        var alpha = await SeedDepartmentAsync("Anestezjologia", "AN");
        await SeedReportAsync(zebra);
        await SeedReportAsync(alpha);

        var result = await CreateSut().GetReports(
            SortedBy(nameof(IncidentReportsGridItem.Department), descending: true));

        Assert.Equal("Zakład patomorfologii", result.Items.First().Department);
    }

    [Fact]
    public async Task SortsByPatientSurnameAscending()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, patientName: "Ewa", patientSurname: "Zielińska");
        await SeedReportAsync(department, patientName: "Piotr", patientSurname: "Adamski");

        var result = await CreateSut().GetReports(
            SortedBy(nameof(IncidentReportsGridItem.PatientFullName), descending: false));

        Assert.Equal("Piotr Adamski", result.Items.First().PatientFullName);
    }

    [Fact]
    public async Task SortsByPatientSurnameDescending()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, patientName: "Ewa", patientSurname: "Zielińska");
        await SeedReportAsync(department, patientName: "Piotr", patientSurname: "Adamski");

        var result = await CreateSut().GetReports(
            SortedBy(nameof(IncidentReportsGridItem.PatientFullName), descending: true));

        Assert.Equal("Ewa Zielińska", result.Items.First().PatientFullName);
    }

    [Fact]
    public async Task SortsByPatientGenderAscending()
    {
        // PatientGender is persisted as a string (HasConversion<string>()), so the database
        // sorts it alphabetically by enum name — "Female" before "Male" — not by the enum's
        // underlying numeric value. Seeded with Male first so a negated sort cannot pass by
        // matching insertion order.
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, patientName: "Marek", gender: Gender.Male);
        await SeedReportAsync(department, patientName: "Ewa", gender: Gender.Female);

        var result = await CreateSut().GetReports(
            SortedBy(nameof(IncidentReportsGridItem.PatientGender), descending: false));

        Assert.StartsWith("Ewa", result.Items.First().PatientFullName);
    }

    [Fact]
    public async Task SortsByPatientGenderDescending()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, patientName: "Marek", gender: Gender.Male);
        await SeedReportAsync(department, patientName: "Ewa", gender: Gender.Female);

        var result = await CreateSut().GetReports(
            SortedBy(nameof(IncidentReportsGridItem.PatientGender), descending: true));

        Assert.StartsWith("Marek", result.Items.First().PatientFullName);
    }

    [Fact]
    public async Task SortsByDateAscending()
    {
        // Seeded with the later date first so a negated sort cannot pass by matching
        // insertion order.
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, patientName: "Nowszy", date: DateTime.Now.AddDays(-1));
        await SeedReportAsync(department, patientName: "Starszy", date: DateTime.Now.AddDays(-30));

        var result = await CreateSut().GetReports(
            SortedBy(nameof(IncidentReportsGridItem.Date), descending: false));

        Assert.StartsWith("Starszy", result.Items.First().PatientFullName);
    }

    [Fact]
    public async Task SortsByDateDescending()
    {
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, patientName: "Nowszy", date: DateTime.Now.AddDays(-1));
        await SeedReportAsync(department, patientName: "Starszy", date: DateTime.Now.AddDays(-30));

        var result = await CreateSut().GetReports(
            SortedBy(nameof(IncidentReportsGridItem.Date), descending: true));

        Assert.StartsWith("Nowszy", result.Items.First().PatientFullName);
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
