using System.Text.RegularExpressions;
using Microsoft.Playwright;
using SafeCare.Data.Entities;
using SafeCare.E2ETests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace SafeCare.E2ETests;

/// <summary>
/// The administrator's user management page.
/// </summary>
public class UserManagementTests(AppFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task KeepsTheBrowserFromAutofillingTheNewUserForm()
    {
        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);
        await page.GotoAsync("/admin/users");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Dodaj nowego użytkownika" }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        // An administrator fills this form in on someone else's behalf. Without these hints a
        // browser reads "text field followed by password fields on a site I have a login for"
        // as a sign-in form and drops the administrator's own saved credentials into it.
        // new-password is the value browsers actually honour: it marks the form as creating a
        // credential rather than entering one, which is what stops the fill.
        await Expect(page.GetByLabel("Hasło", new() { Exact = true }))
            .ToHaveAttributeAsync("autocomplete", "new-password");
        await Expect(page.GetByLabel("Potwierdź hasło"))
            .ToHaveAttributeAsync("autocomplete", "new-password");
        await Expect(page.GetByLabel("Nazwa użytkownika"))
            .ToHaveAttributeAsync("autocomplete", "off");
    }

    [Fact]
    public async Task ResetsAUsersTwoFactorAuthenticationAndEmailsThem()
    {
        // The user has lost their phone: after the reset they sign in with their password
        // alone and are led straight back through setup.
        await Fixture.CreateUserAsync("zgubiony", "Haslo123!", AppRoles.User, twoFactor: true);
        using var mail = new MailPitClient(Fixture.MailPitApiUrl);

        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);

        // Reach the page through the menu rather than a fresh load, so the circuit that
        // LoginAsAdminAsync waited for is the one handling the clicks below.
        await page.GetByRole(AriaRole.Button, new() { Name = "System Administrator" }).First.ClickAsync();
        await page.GetByText("Zarządzanie użytkownikami").ClickAsync();

        var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = "zgubiony" });
        await Expect(row.GetByText("Włączona")).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await row.GetByRole(AriaRole.Button, new() { Name = "Resetuj 2FA" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Tak, resetuj" }).ClickAsync();

        await Expect(page.GetByText("Weryfikacja dwuetapowa została zresetowana.")).ToBeVisibleAsync();
        await Expect(row.GetByText("Nieskonfigurowana")).ToBeVisibleAsync();

        var message = await mail.WaitForMessageAsync("Zresetowano weryfikację dwuetapową", TimeSpan.FromSeconds(60));
        Assert.Contains(message.Bcc ?? [], a => a.Address == "zgubiony@szpital.example");

        var user = await NewPageAsync();
        await user.GotoAsync("/login");
        await SubmitLoginFormAsync(user, "zgubiony", "Haslo123!");
        await Expect(user).ToHaveURLAsync(new Regex("/account/2fa-setup$"));
    }
}
