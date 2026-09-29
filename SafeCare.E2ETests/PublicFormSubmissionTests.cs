using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.Data.Entities;
using SafeCare.E2ETests.Infrastructure;

namespace SafeCare.E2ETests;

public class PublicFormSubmissionTests(AppFixture fixture) : E2ETestBase(fixture)
{
    // All three exist in SeedData.sql, which the application loads on startup.
    private const string Department = "Chirurgia";
    private const string Category = "Działalność kliniczna";
    private const string IncidentType = "błędna diagnoza";

    private async Task<IncidentReport?> FindReportAsync(string description)
    {
        await using var db = new AppDbContext(Fixture.DbOptions);
        return await db.IncidentReports
            .Include(r => r.Department)
            .Include(r => r.IncidentDefinitions)
            .SingleOrDefaultAsync(r => r.IncidentDescription == description);
    }

    [Fact]
    public async Task StoresAnAnonymousReportSubmittedThroughTheForm()
    {
        var description = $"Zgłoszenie testowe {Guid.NewGuid()}";
        var form = new PublicFormPage(await NewPageAsync());

        await form.OpenAsync();
        await form.FillMinimalAsync(description, Department, Category, IncidentType);

        // The bot defence rejects anything submitted within five seconds of load.
        await Task.Delay(MinimumFormFillTime);
        await form.SubmitAsync();
        await form.ExpectSubmissionConfirmedAsync();

        var stored = await FindReportAsync(description);

        Assert.NotNull(stored);
        Assert.Equal(Department, stored.Department.Name);
        Assert.Equal(IncidentType, Assert.Single(stored.IncidentDefinitions).Name);
        Assert.Equal(DateTime.Today.AddDays(-1).AddHours(10).AddMinutes(30), stored.Date);
        Assert.Equal(SafeCare.Enums.ReportStatus.New, stored.Status);

        // Anonymous by design: nothing about the reporter was entered, so nothing is stored.
        Assert.True(string.IsNullOrEmpty(stored.Name));
        Assert.True(string.IsNullOrEmpty(stored.Surname));
    }

    [Fact]
    public async Task StoresAReportCarryingReporterDetails()
    {
        var description = $"Zgłoszenie imienne {Guid.NewGuid()}";
        var form = new PublicFormPage(await NewPageAsync());

        await form.OpenAsync();
        await form.FillMinimalAsync(description, Department, Category, IncidentType);

        // Diacritics in names once made the validator reject the whole submission.
        await form.FillReporterAsync("Paweł", "Wiśniewski");

        await Task.Delay(MinimumFormFillTime);
        await form.SubmitAsync();
        await form.ExpectSubmissionConfirmedAsync();

        var stored = await FindReportAsync(description);

        Assert.NotNull(stored);
        Assert.Equal("Paweł", stored.Name);
        Assert.Equal("Wiśniewski", stored.Surname);
    }

    [Fact]
    public async Task ClearsTheFormAfterASuccessfulSubmission()
    {
        var description = $"Zgłoszenie do wyczyszczenia {Guid.NewGuid()}";
        var form = new PublicFormPage(await NewPageAsync());

        await form.OpenAsync();
        await form.FillMinimalAsync(description, Department, Category, IncidentType);

        await Task.Delay(MinimumFormFillTime);
        await form.SubmitAsync();
        await form.ExpectSubmissionConfirmedAsync();

        // Left filled, the form would invite an accidental duplicate submission.
        await Microsoft.Playwright.Assertions.Expect(form.DescriptionField).ToHaveValueAsync("");
    }
}
