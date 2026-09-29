using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.E2ETests.Infrastructure;

namespace SafeCare.E2ETests;

public class EmailNotificationTests(AppFixture fixture) : E2ETestBase(fixture)
{
    // The seeded administrator's address - see the HasData seed in User.cs.
    private const string AdminAddress = "system@admin.pl";

    // All three exist in SeedData.sql, which the application loads on startup.
    private const string Department = "Chirurgia";
    private const string Category = "Działalność kliniczna";
    private const string IncidentType = "błędna diagnoza";

    private async Task SetAdminNotificationsAsync(bool enabled)
    {
        await using var db = new AppDbContext(Fixture.DbOptions);
        var admin = await db.Users.SingleAsync(u => u.UserName == "admin");
        admin.ReceiveEmailNotifications = enabled;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task DeliversANotificationWithRecipientsHiddenInBcc()
    {
        // Opt the seeded administrator in so there is a recipient. The setting outlives this
        // test (the database is shared), so it is switched off again afterwards.
        await SetAdminNotificationsAsync(true);

        try
        {
            using var mail = new MailPitClient(Fixture.MailPitApiUrl);
            await mail.DeleteAllAsync();

            var token = Guid.NewGuid();
            var description = $"Zgłoszenie z powiadomieniem {token}";
            var form = new PublicFormPage(await NewPageAsync());
            await form.OpenAsync();
            await form.FillMinimalAsync(description, Department, Category, IncidentType);
            await Task.Delay(MinimumFormFillTime);
            await form.SubmitAsync();
            await form.ExpectSubmissionConfirmedAsync();

            await using var db = new AppDbContext(Fixture.DbOptions);
            var reportId = await db.IncidentReports
                .Where(r => r.IncidentDescription == description)
                .Select(r => r.Id)
                .SingleAsync();

            var message = await mail.WaitForMessageAsync($"#{reportId}", TimeSpan.FromSeconds(60));

            Assert.Equal($"[SafeCare] Nowe zgłoszenie zdarzenia #{reportId}", message.Subject);

            // Staff addresses must never appear in a header other recipients can read.
            Assert.NotNull(message.Bcc);
            Assert.Contains(message.Bcc, a => a.Address == AdminAddress);
            Assert.DoesNotContain(message.To ?? [], a => a.Address == AdminAddress);

            // The mail is about this report, not merely any report.
            Assert.Contains(token.ToString(), await mail.GetHtmlBodyAsync(message.Id));
        }
        finally
        {
            await SetAdminNotificationsAsync(false);
        }
    }

    [Fact]
    public async Task SendsNoNotificationWhenNobodyHasOptedIn()
    {
        await SetAdminNotificationsAsync(false);

        using var mail = new MailPitClient(Fixture.MailPitApiUrl);
        await mail.DeleteAllAsync();

        var description = $"Zgłoszenie bez powiadomienia {Guid.NewGuid()}";
        var form = new PublicFormPage(await NewPageAsync());
        await form.OpenAsync();
        await form.FillMinimalAsync(description, Department, Category, IncidentType);
        await Task.Delay(MinimumFormFillTime);
        await form.SubmitAsync();
        await form.ExpectSubmissionConfirmedAsync();

        await using var db = new AppDbContext(Fixture.DbOptions);
        var reportId = await db.IncidentReports
            .Where(r => r.IncidentDescription == description)
            .Select(r => r.Id)
            .SingleAsync();

        // Absence cannot be polled for. The queue drains within a moment when there is
        // something to send, so a few seconds of silence is meaningful.
        await Assert.ThrowsAsync<TimeoutException>(
            () => mail.WaitForMessageAsync($"#{reportId}", TimeSpan.FromSeconds(5)));
    }
}
