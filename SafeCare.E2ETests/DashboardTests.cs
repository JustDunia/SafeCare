using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using SafeCare.Data;
using SafeCare.Data.Entities;
using SafeCare.E2ETests.Infrastructure;
using SafeCare.Enums;
using static Microsoft.Playwright.Assertions;

namespace SafeCare.E2ETests;

/// <summary>
/// Dashboard grid behaviour: ordering, filtering, bookmarkable state and opening a report.
/// </summary>
/// <remarks>
/// The application seeds only dictionary data, so these tests create the reports they need.
/// That is deliberate: a grid test that leans on demo filler asserts against whatever the
/// seed script happens to contain, and silently changes meaning when that file is edited.
/// Rows accumulate across the tests in this class — they share one database and one app —
/// so every assertion here is relative to what the test itself created, never to an absolute
/// row count.
/// </remarks>
public class DashboardTests(AppFixture fixture) : E2ETestBase(fixture)
{
    private const int DefaultPageSize = 25;

    // Column order of the grid: Lp., Zgłaszający, Pacjent, Wiek, Płeć, Data zdarzenia, Oddział, ...
    private const int DepartmentColumn = 6;

    /// <summary>
    /// Body rows of the grid. The header row holds column headers rather than cells, so the
    /// filter keeps it out.
    /// </summary>
    private static ILocator DataRows(IPage page) =>
        page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Cell) });

    /// <summary>The pager caption, e.g. "1-25 z 40"; the grid's own way of stating its total.</summary>
    private static ILocator PagerCaption(IPage page) =>
        page.GetByText(new Regex(@"^\d+-\d+ z \d+$"));

    private static async Task<int> TotalReportsAsync(IPage page)
    {
        var caption = await PagerCaption(page).InnerTextAsync();
        return int.Parse(caption[(caption.LastIndexOf(' ') + 1)..]);
    }

    /// <summary>
    /// Writes reports straight to the database. This is test setup, not the behaviour under
    /// test — the grid is what these tests exercise, and driving the public form once per row
    /// would add the five-second bot-defence delay to every single one.
    /// </summary>
    private async Task SeedReportsAsync(int count, string departmentName)
    {
        await using var db = new AppDbContext(Fixture.DbOptions);

        var department = await db.Departments.FirstAsync(d => d.Name == departmentName);
        var definition = await db.IncidentDefinitions.FirstAsync();

        for (var i = 0; i < count; i++)
        {
            db.IncidentReports.Add(new IncidentReport(
                "Anna", "Kowalska", "123456789", "anna@example.com",
                "Piotr", "Wiśniewski", new DateTime(1980, 5, 12), Gender.Male,
                null, null, DateTime.Now.AddDays(-1),
                department, [definition], null, $"Zgłoszenie testowe {Guid.NewGuid()}"));
        }

        await db.SaveChangesAsync();
    }

    private async Task<IPage> OpenDashboardAsync(string query = "")
    {
        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);

        // Rows are fetched over the circuit, so their arrival also proves the page is live.
        await page.GotoAsync("/dashboard" + query);
        await Expect(DataRows(page).First).ToBeVisibleAsync();

        return page;
    }

    [Fact]
    public async Task ShowsReportsNewestFirstOnAFullFirstPage()
    {
        await SeedReportsAsync(DefaultPageSize + 5, "Ortopedia");

        var page = await OpenDashboardAsync();

        await Expect(DataRows(page)).ToHaveCountAsync(DefaultPageSize);

        var ids = (await DataRows(page).Locator("td:first-child").AllInnerTextsAsync())
            .Select(int.Parse)
            .ToList();

        Assert.Equal(ids.OrderByDescending(id => id), ids);
    }

    [Fact]
    public async Task KeepsFilterStateInTheUrlSoTheViewIsBookmarkable()
    {
        // The department filter is round-tripped through the query string by design.
        const string department = "Radiologia";
        await SeedReportsAsync(3, department);
        await SeedReportsAsync(3, "Neurologia");

        var page = await OpenDashboardAsync($"?department={department}");

        // Opening a filtered address must show that filter, both in the input and in the rows.
        await Expect(page.GetByLabel("Oddział")).ToHaveValueAsync(department);
        await Expect(DataRows(page).First.GetByRole(AriaRole.Cell).Nth(DepartmentColumn)).ToHaveTextAsync(department);

        var otherDepartments = DataRows(page).Filter(new()
        {
            HasNot = page.GetByRole(AriaRole.Cell, new() { Name = department, Exact = true })
        });
        await Expect(otherDepartments).ToHaveCountAsync(0);

        // The filter must actually narrow the set, not merely decorate the address bar.
        var filteredTotal = await TotalReportsAsync(page);
        var unfiltered = await OpenDashboardAsync();
        Assert.True(filteredTotal < await TotalReportsAsync(unfiltered));
    }

    [Fact]
    public async Task WritesAppliedFiltersToTheUrlAndReproducesThemFromIt()
    {
        const string department = "Kardiologia";
        await SeedReportsAsync(3, department);
        await SeedReportsAsync(3, "Pediatria");

        var page = await OpenDashboardAsync();

        // Applying a filter in the UI must put it in the address...
        await page.GetByLabel("Oddział").FillAsync(department);
        await page.GetByLabel("Oddział").PressAsync("Enter");

        await Expect(page).ToHaveURLAsync(new Regex($"department={department}"));
        await Expect(DataRows(page).First.GetByRole(AriaRole.Cell).Nth(DepartmentColumn)).ToHaveTextAsync(department);
        var filteredCaption = await PagerCaption(page).InnerTextAsync();

        // ...so that a fresh session opening that very address lands on the same view.
        var bookmarked = await NewPageAsync();
        await LoginAsAdminAsync(bookmarked);
        await bookmarked.GotoAsync(page.Url);

        await Expect(DataRows(bookmarked).First.GetByRole(AriaRole.Cell).Nth(DepartmentColumn)).ToHaveTextAsync(department);
        await Expect(PagerCaption(bookmarked)).ToHaveTextAsync(filteredCaption);
    }

    [Fact]
    public async Task OpensAReportFromTheGrid()
    {
        await SeedReportsAsync(1, "Onkologia");

        var page = await OpenDashboardAsync();

        var row = DataRows(page).First;
        var id = await row.GetByRole(AriaRole.Cell).First.InnerTextAsync();
        var department = await row.GetByRole(AriaRole.Cell).Nth(DepartmentColumn).InnerTextAsync();

        // A single click is reserved for selecting text; only a double click opens the report.
        await row.DblClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex($"/details/{id}$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = $"Zgłoszenie #{id}" })).ToBeVisibleAsync();

        // The details belong to the row that was clicked, not merely to some report.
        await Expect(page.GetByText(department, new() { Exact = true })).ToBeVisibleAsync();
    }
}
