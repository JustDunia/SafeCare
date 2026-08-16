using Microsoft.EntityFrameworkCore;
using SafeCare.Data.Entities;
using SafeCare.Dtos;
using SafeCare.Enums;
using SafeCare.Mappings;
using SafeCare.Services;
using SafeCare.Tests.Integration.Infrastructure;

namespace SafeCare.Tests.Integration;

public class IncidentReportServiceNotificationTests(PostgresFixture fixture) : IntegrationTestBase(fixture)
{
    private IncidentReportService CreateSut() => new(DbFactory, EmailQueue);

    private async Task AddUserAsync(string userName, string? email, bool receivesNotifications)
    {
        await using var db = CreateDbContext();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email?.ToUpperInvariant(),
            FirstName = "Test",
            LastName = "User",
            ReceiveEmailNotifications = receivesNotifications,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> SubmitReportAsync()
    {
        var department = await SeedDepartmentAsync();
        var definition = await SeedDefinitionAsync();

        return await CreateSut().CreateReport(new IncidentReportDto
        {
            PatientGender = Gender.Female,
            Date = DateTime.Now.AddDays(-1),
            Department = department.ToDto(),
            SelectedIncidentDefinitions = [definition.ToDto()],
            IncidentDescription = "Opis zdarzenia."
        });
    }

    [Fact]
    public async Task NotifiesOnlyUsersWhoOptedIn()
    {
        await AddUserAsync("optin", "optin@szpital.pl", receivesNotifications: true);
        await AddUserAsync("optout", "optout@szpital.pl", receivesNotifications: false);

        await SubmitReportAsync();

        var message = Assert.Single(EmailQueue.Sent);
        Assert.Equal(["optin@szpital.pl"], message.BccRecipients);
    }

    [Fact]
    public async Task SkipsOptedInUsersWithoutAnEmailAddress()
    {
        await AddUserAsync("noaddress", null, receivesNotifications: true);

        await SubmitReportAsync();

        Assert.Empty(EmailQueue.Sent);
    }

    [Fact]
    public async Task SendsNothingWhenNobodyOptedIn()
    {
        await AddUserAsync("optout", "optout@szpital.pl", receivesNotifications: false);

        await SubmitReportAsync();

        Assert.Empty(EmailQueue.Sent);
    }

    [Fact]
    public async Task StillStoresTheReportWhenTheMailQueueFails()
    {
        // Deliberate design decision: "a broken mail server must not fail a patient's report".
        // If this test starts failing, the swallow-and-log in CreateReport was removed.
        await AddUserAsync("optin", "optin@szpital.pl", receivesNotifications: true);
        EmailQueue.ThrowOnEnqueue = true;

        var id = await SubmitReportAsync();

        await using var db = CreateDbContext();
        Assert.True(await db.IncidentReports.AnyAsync(r => r.Id == id));
    }
}
