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
        // Without a correctly escaped ILIKE pattern, searching "100%" can go wrong in two
        // directions: it can match nothing at all (the escape sequence is taken literally
        // instead of unescaping the wildcard), or it can match the wrong row (an unescaped "%"
        // still behaves as a wildcard). Asserting the actual matched row, not just the count,
        // catches both failure modes.
        var department = await SeedDepartmentAsync();
        await SeedReportAsync(department, name: "100%", surname: "Pewny");
        await SeedReportAsync(department, name: "Anna", surname: "Kowalska");

        var result = await CreateSut().GetReports(
            Request(new IncidentReportFilter { FullName = "100%" }));

        Assert.Equal(1, result.ItemTotalCount);
        Assert.Equal("100% Pewny", result.Items.Single().FullName);
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
