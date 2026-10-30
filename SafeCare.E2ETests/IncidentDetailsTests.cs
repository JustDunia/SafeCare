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
/// The report details page itself, as opposed to the grid that leads to it.
/// </summary>
public class IncidentDetailsTests(AppFixture fixture) : E2ETestBase(fixture)
{
    /// <summary>
    /// Writes one report straight to the database and returns its id. Test setup, not the
    /// behaviour under test — see DashboardTests.SeedReportsAsync for why it bypasses the
    /// public form.
    /// </summary>
    private async Task<int> SeedReportAsync(string departmentName)
    {
        await using var db = new AppDbContext(Fixture.DbOptions);

        var department = await db.Departments.FirstAsync(d => d.Name == departmentName);
        var definition = await db.IncidentDefinitions.FirstAsync();

        var report = new IncidentReport(
            "Anna", "Kowalska", "123456789", "anna@example.com",
            "Piotr", "Wiśniewski", new DateTime(1980, 5, 12), Gender.Male,
            null, null, DateTime.Now.AddDays(-1),
            department, [definition], null, $"Zgłoszenie testowe {Guid.NewGuid()}");

        db.IncidentReports.Add(report);
        await db.SaveChangesAsync();

        return report.Id;
    }

    [Fact]
    public async Task LeavesForTheListAfterDeletingAReportOpenedDirectly()
    {
        var id = await SeedReportAsync("Dermatologia");

        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);

        // A fresh tab in the same session, whose first navigation is the report itself: its
        // history holds nothing to step back to, exactly as when a link is pasted or a
        // bookmark opened. Leaving has to be a navigation of its own, not a step backwards.
        var direct = await page.Context.NewPageAsync();
        await direct.GotoAsync($"/details/{id}");
        await Expect(direct.GetByRole(AriaRole.Heading, new() { Name = $"Zgłoszenie #{id}" }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        await direct.GetByRole(AriaRole.Button, new() { Name = "Otwórz menu" }).ClickAsync();
        await direct.GetByText("Usuń", new() { Exact = true }).ClickAsync();
        await direct.GetByRole(AriaRole.Button, new() { Name = "Tak" }).ClickAsync();

        await Expect(direct).ToHaveURLAsync(new Regex("/dashboard$"));
    }
}
