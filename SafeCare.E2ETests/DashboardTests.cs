using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SafeCare.E2ETests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace SafeCare.E2ETests;

public class DashboardTests(AppFixture fixture) : E2ETestBase(fixture)
{
    // The demo data seeded on startup - see SeedData.sql: 200 reports over ten departments.
    private const int SeededReportCount = 200;
    private const int DefaultPageSize = 25;

    // Column order of the grid: Lp., Zgłaszający, Pacjent, Wiek, Płeć, Data zdarzenia, Oddział, ...
    private const int DepartmentColumn = 6;

    /// <summary>
    /// Body rows of the grid. The header row holds column headers rather than cells, so the
    /// filter keeps it out.
    /// </summary>
    private static ILocator DataRows(IPage page) =>
        page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Cell) });

    /// <summary>The pager caption, e.g. "1-25 z 200"; the grid's own way of stating its total.</summary>
    private static ILocator PagerCaption(IPage page) =>
        page.GetByText(new Regex(@"^\d+-\d+ z \d+$"));

    private static async Task<int> TotalReportsAsync(IPage page)
    {
        var caption = await PagerCaption(page).InnerTextAsync();
        return int.Parse(caption[(caption.LastIndexOf(' ') + 1)..]);
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
    public async Task ShowsTheSeededReportsNewestFirst()
    {
        var page = await OpenDashboardAsync();

        await Expect(DataRows(page)).ToHaveCountAsync(DefaultPageSize);
        Assert.True(await TotalReportsAsync(page) >= SeededReportCount);

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
        var page = await OpenDashboardAsync($"?department={department}");

        // Opening a filtered address must show that filter, both in the input and in the rows.
        await Expect(page.GetByLabel("Oddział")).ToHaveValueAsync(department);
        await Expect(DataRows(page).First.GetByRole(AriaRole.Cell).Nth(DepartmentColumn)).ToHaveTextAsync(department);

        var otherDepartments = DataRows(page).Filter(new()
        {
            HasNot = page.GetByRole(AriaRole.Cell, new() { Name = department, Exact = true })
        });
        await Expect(otherDepartments).ToHaveCountAsync(0);
        Assert.True(await TotalReportsAsync(page) < SeededReportCount);
    }

    [Fact]
    public async Task WritesAppliedFiltersToTheUrlAndReproducesThemFromIt()
    {
        var page = await OpenDashboardAsync();

        // Applying a filter in the UI must put it in the address...
        await page.GetByLabel("Oddział").FillAsync("Kardiologia");
        await page.GetByLabel("Oddział").PressAsync("Enter");

        await Expect(page).ToHaveURLAsync(new Regex("department=Kardiologia"));
        await Expect(DataRows(page).First.GetByRole(AriaRole.Cell).Nth(DepartmentColumn)).ToHaveTextAsync("Kardiologia");
        var filteredCaption = await PagerCaption(page).InnerTextAsync();

        // ...so that a fresh session opening that very address lands on the same view.
        var bookmarked = await NewPageAsync();
        await LoginAsAdminAsync(bookmarked);
        await bookmarked.GotoAsync(page.Url);

        await Expect(DataRows(bookmarked).First.GetByRole(AriaRole.Cell).Nth(DepartmentColumn)).ToHaveTextAsync("Kardiologia");
        await Expect(PagerCaption(bookmarked)).ToHaveTextAsync(filteredCaption);
    }

    [Fact]
    public async Task OpensAReportFromTheGrid()
    {
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
