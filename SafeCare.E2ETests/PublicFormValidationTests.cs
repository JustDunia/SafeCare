using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.E2ETests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace SafeCare.E2ETests;

public class PublicFormValidationTests(AppFixture fixture) : E2ETestBase(fixture)
{
    // All three exist in SeedData.sql, which the application loads on startup.
    private const string Department = "Chirurgia";
    private const string Category = "Działalność kliniczna";
    private const string IncidentType = "błędna diagnoza";

    /// <summary>How long a rejected submission is given to (not) produce any visible effect.</summary>
    private static readonly TimeSpan SilenceObservationTime = TimeSpan.FromSeconds(2);

    private async Task<int> ReportCountAsync()
    {
        await using var db = new AppDbContext(Fixture.DbOptions);
        return await db.IncidentReports.CountAsync();
    }

    private async Task<bool> ReportExistsAsync(string description)
    {
        await using var db = new AppDbContext(Fixture.DbOptions);
        return await db.IncidentReports.AnyAsync(r => r.IncidentDescription == description);
    }

    [Fact]
    public async Task ShowsPolishValidationMessagesForAnEmptyForm()
    {
        var before = await ReportCountAsync();
        var form = new PublicFormPage(await NewPageAsync());
        await form.OpenAsync();

        // Validation runs after the bot check, so the form has to look human first.
        await Task.Delay(MinimumFormFillTime);
        await form.SubmitAsync();

        await Expect(form.Page.GetByText("Data jest wymagana")).ToBeVisibleAsync();
        await Expect(form.Page.GetByText("Czas jest wymagany")).ToBeVisibleAsync();
        await Expect(form.Page.GetByText("Miejsce zdarzenia jest wymagane")).ToBeVisibleAsync();
        await Expect(form.Page.GetByText("Należy wybrać co najmniej jeden rodzaj zdarzenia lub podać własny opis"))
            .ToBeVisibleAsync();
        await Expect(form.Page.GetByText("Opis zdarzenia jest wymagany")).ToBeVisibleAsync();

        Assert.Equal(before, await ReportCountAsync());
    }

    [Fact]
    public async Task ShowsPolishMessagesForMalformedOptionalFields()
    {
        var before = await ReportCountAsync();
        var form = new PublicFormPage(await NewPageAsync());
        await form.OpenAsync();

        // Reporter details are optional, but once entered they must be well-formed.
        await form.FillReporterAsync("Anna5", "K");
        await form.FillPhoneAsync("12345");
        await form.FillEmailAsync("to-nie-jest-adres");

        await Task.Delay(MinimumFormFillTime);
        await form.SubmitAsync();

        await Expect(form.Page.GetByText("Imię może zawierać tylko litery")).ToBeVisibleAsync();
        await Expect(form.Page.GetByText("Nazwisko musi mieć od 2 do 50 znaków")).ToBeVisibleAsync();
        await Expect(form.Page.GetByText("Numer telefonu musi być poprawnym polskim numerem telefonu"))
            .ToBeVisibleAsync();
        await Expect(form.Page.GetByText("Adres e-mail musi być poprawny")).ToBeVisibleAsync();

        Assert.Equal(before, await ReportCountAsync());
    }

    [Fact]
    public async Task SilentlyDiscardsASubmissionSentTooQuickly()
    {
        // The bot defence deliberately gives no feedback - a bot must not learn why it failed.
        // The report simply must not exist, and the page must look as if nothing happened.
        var description = $"Zbyt szybkie zgłoszenie {Guid.NewGuid()}";
        var before = await ReportCountAsync();

        var form = new PublicFormPage(await NewPageAsync());
        await form.OpenAsync();
        await form.FillMinimalAsync(description, Department, Category, IncidentType);

        // No waiting: the form is complete and valid, only the timing is wrong.
        await form.SubmitAsync();

        await Task.Delay(SilenceObservationTime);

        await form.ExpectNoSnackbarAsync();
        await Expect(form.Page.GetByText("jest wymagan")).ToHaveCountAsync(0);
        await Expect(form.DescriptionField).ToHaveValueAsync(description);
        Assert.Equal(before, await ReportCountAsync());
        Assert.False(
            await ReportExistsAsync(description),
            "The report was stored - was the form filled in and submitted within the 5 second window?");

        // Control: the very same form, submitted once it has been open long enough, goes through.
        // Without this the assertions above would also pass if the form were simply broken.
        await Task.Delay(MinimumFormFillTime);
        await form.SubmitAsync();
        await form.ExpectSubmissionConfirmedAsync();
        Assert.True(await ReportExistsAsync(description));
    }

    [Fact]
    public async Task SilentlyDiscardsASubmissionThatFilledAHoneypotField()
    {
        var description = $"Zgłoszenie bota {Guid.NewGuid()}";
        var before = await ReportCountAsync();

        var form = new PublicFormPage(await NewPageAsync());
        await form.OpenAsync();
        await form.FillMinimalAsync(description, Department, Category, IncidentType);

        // A human never sees this field; a form-filling bot cannot resist it.
        await form.FillHoneypotAsync("website", "http://spam.example");

        // Wait out the timing check so that the honeypot is the only thing that can reject this.
        await Task.Delay(MinimumFormFillTime);
        await form.SubmitAsync();

        await Task.Delay(SilenceObservationTime);

        await form.ExpectNoSnackbarAsync();
        await Expect(form.Page.GetByText("jest wymagan")).ToHaveCountAsync(0);
        Assert.Equal(before, await ReportCountAsync());
        Assert.False(await ReportExistsAsync(description));

        // Control: with the honeypot emptied the same submission is accepted.
        await form.ClearHoneypotAsync("website");
        await form.SubmitAsync();
        await form.ExpectSubmissionConfirmedAsync();
        Assert.True(await ReportExistsAsync(description));
    }
}
