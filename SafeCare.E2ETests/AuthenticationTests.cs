using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Playwright;
using SafeCare.Data.Entities;
using SafeCare.E2ETests.Infrastructure;
using SafeCare.Tests;
using static Microsoft.Playwright.Assertions;

namespace SafeCare.E2ETests;

public class AuthenticationTests(AppFixture fixture) : E2ETestBase(fixture)
{
    // The seeded administrator's display name - see the HasData seed in User.cs.
    private const string AdminDisplayName = "System Administrator";

    private const string NewUserPassword = "Haslo123!";

    [Fact]
    public async Task SignsInWithValidCredentialsAndLandsOnTheDashboard()
    {
        var page = await NewPageAsync();

        await LoginAsAdminAsync(page);

        await Expect(page).ToHaveURLAsync(new Regex("/dashboard$"));
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = AdminDisplayName }).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task RejectsInvalidCredentialsAndReturnsToLoginWithAnError()
    {
        var page = await NewPageAsync();
        await page.GotoAsync("/login");

        await SubmitLoginFormAsync(page, "admin", "ZleHaslo123!");

        await Expect(page).ToHaveURLAsync(new Regex("/login\\?error=InvalidCredentials$"));
        await Expect(page.GetByText("Nieprawidłowa nazwa użytkownika lub hasło.")).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData("/account", "Ustawienia konta")]
    [InlineData("/details/5", "Zgłoszenie #5")]
    public async Task SendsAnAnonymousVisitorToLoginAndBackToTheRequestedPage(string path, string heading)
    {
        // The returnUrl round trip has a sharp edge: /signin only accepts paths beginning with
        // "/", while ToBaseRelativePath yields none. A regression there drops the redirect
        // silently and the user lands on the dashboard instead.
        var page = await NewPageAsync();

        await page.GotoAsync(path);
        await Expect(page).ToHaveURLAsync(new Regex("/login\\?"));

        var query = HttpUtility.ParseQueryString(new Uri(page.Url).Query);
        Assert.Equal(path, query["returnUrl"]);

        await SubmitLoginFormAsync(page, "admin", "Admin123!");
        await page.WaitForURLAsync(url => url.Contains("/login/2fa"));
        await SubmitTwoFactorCodeAsync(page, AdminCode());

        await Expect(page).ToHaveURLAsync(new Regex(Regex.Escape(path) + "$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = heading })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task IgnoresAReturnUrlPointingToAnotherSite()
    {
        // Otherwise the login form is an open redirect: a crafted link would forward a freshly
        // authenticated user to a site of the attacker's choosing.
        var page = await NewPageAsync();
        await page.GotoAsync("/login?returnUrl=" + Uri.EscapeDataString("https://evil.example/phish"));

        await SubmitLoginFormAsync(page, "admin", "Admin123!");
        await page.WaitForURLAsync(url => url.Contains("/login/2fa"));
        await SubmitTwoFactorCodeAsync(page, AdminCode());

        await Expect(page).ToHaveURLAsync(new Regex("/dashboard$"));
        Assert.StartsWith(Fixture.BaseUrl, page.Url);
    }

    [Fact]
    public async Task OpensNothingUntilTheCodeFromTheAppIsGiven()
    {
        // The right password alone must not be a session: it only earns the code step.
        var page = await NewPageAsync();
        await page.GotoAsync("/login");
        await SubmitLoginFormAsync(page, "admin", "Admin123!");
        await Expect(page).ToHaveURLAsync(new Regex(@"/login/2fa\?"));

        await page.GotoAsync("/dashboard");

        // The server's own challenge spells it "ReturnUrl"; the login page reads either.
        await Expect(page).ToHaveURLAsync(new Regex(@"/login\?returnUrl=", RegexOptions.IgnoreCase));
    }

    [Fact]
    public async Task RejectsAWrongCodeAndStaysOnTheCodeStep()
    {
        var page = await NewPageAsync();
        await page.GotoAsync("/login");
        await SubmitLoginFormAsync(page, "admin", "Admin123!");
        await page.WaitForURLAsync(url => url.Contains("/login/2fa"));

        // Identity accepts codes from neighbouring time steps too; one from a day ago is
        // wrong for certain.
        var wrongCode = Totp.Compute(Fixture.AdminAuthenticatorKey, DateTimeOffset.UtcNow.AddDays(-1));
        await SubmitTwoFactorCodeAsync(page, wrongCode);

        await Expect(page).ToHaveURLAsync(new Regex(@"/login/2fa\?.*error=InvalidCode"));
        await Expect(page.GetByText("Nieprawidłowy kod. Spróbuj ponownie.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SignsInWithARecoveryCodeInsteadOfTheApp()
    {
        // The way back in for an administrator whose phone is gone — nobody else can reset
        // their two-factor authentication for them.
        var page = await NewPageAsync();
        await page.GotoAsync("/login");
        await SubmitLoginFormAsync(page, "admin", "Admin123!");
        await page.WaitForURLAsync(url => url.Contains("/login/2fa"));

        await SubmitTwoFactorCodeAsync(page, Fixture.TakeAdminRecoveryCode());

        await Expect(page).ToHaveURLAsync(new Regex("/dashboard$"));
    }

    [Fact]
    public async Task MakesANewUserSetUpTheAuthenticatorAppBeforeAnythingElse()
    {
        await Fixture.CreateUserAsync("nowy2fa", NewUserPassword, AppRoles.User);
        var page = await NewPageAsync();
        await page.GotoAsync("/login");

        await SubmitLoginFormAsync(page, "nowy2fa", NewUserPassword);

        await Expect(page).ToHaveURLAsync(new Regex("/account/2fa-setup$"));

        // Until it is set up, every other page leads back here.
        await page.GotoAsync("/dashboard");
        await Expect(page).ToHaveURLAsync(new Regex("/account/2fa-setup$"));

        // The key shown for typing in by hand — the same secret the QR code carries.
        var key = await page.GetByTestId("authenticator-key").InnerTextAsync();
        await ConfirmAuthenticatorSetupAsync(page, key);

        await Expect(page.GetByTestId("recovery-codes").Locator("code")).ToHaveCountAsync(10);

        await page.GetByRole(AriaRole.Button, new() { Name = "Kody zapisane — zaloguj ponownie" }).ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex(@"/login\?info=TwoFactorEnabled$"));
        await Expect(page.GetByText("Weryfikacja dwuetapowa jest skonfigurowana.", new() { Exact = false })).ToBeVisibleAsync();

        await SubmitLoginFormAsync(page, "nowy2fa", NewUserPassword);
        await page.WaitForURLAsync(url => url.Contains("/login/2fa"));
        await SubmitTwoFactorCodeAsync(page, Totp.Compute(key));

        await Expect(page).ToHaveURLAsync(new Regex("/dashboard$"));
    }

    /// <summary>
    /// Enters a code on the setup page and confirms it. That form is handled by the circuit,
    /// so a click before the circuit attaches does nothing useful; it is retried, with a
    /// fresh code each time, until the recovery codes appear.
    /// </summary>
    private static async Task ConfirmAuthenticatorSetupAsync(IPage page, string key)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);

        while (true)
        {
            await page.GetByLabel("Kod z aplikacji").FillAsync(Totp.Compute(key));
            await page.GetByRole(AriaRole.Button, new() { Name = "Potwierdź" }).ClickAsync();

            try
            {
                await page.GetByTestId("recovery-codes").WaitForAsync(new() { Timeout = 3_000 });
                return;
            }
            catch (TimeoutException) when (DateTime.UtcNow < deadline)
            {
                // circuit not connected yet - try again
            }
        }
    }

    [Fact]
    public async Task BlocksAnonymousAccessToTheDashboard()
    {
        var page = await NewPageAsync();

        await page.GotoAsync("/dashboard");

        await Expect(page).ToHaveURLAsync(new Regex("/login\\?"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Logowanie" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SkipsTheLoginPageForAnAlreadySignedInUser()
    {
        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);

        await page.GotoAsync("/login");

        await Expect(page).ToHaveURLAsync(new Regex("/dashboard$"));
    }

    [Fact]
    public async Task EndsTheSessionOnSignOut()
    {
        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);

        // Let the dashboard finish loading first: navigating away while it is still starting up
        // can abort the follow-up navigation below. The pager caption is the settled signal
        // here, not a data row — this test creates no reports and the application seeds none.
        await Expect(PagerCaption(page)).ToBeVisibleAsync();

        // Signing out is a form POST. The button that submits it lives in the user menu, whose
        // click path OpensTheUserMenuWithAMouseClick covers; this test is about the endpoint, so
        // it sends the same POST through the browser context, sharing the session cookie.
        var response = await page.Context.APIRequest.PostAsync(
            "/signout",
            new() { MaxRedirects = 0 });

        // Signing out lands on the public incident form, not on the login page.
        Assert.Equal(302, response.Status);
        Assert.Equal("/", response.Headers["location"]);

        // The cookie is gone, so the protected page is out of reach again. A fresh page in the
        // same context asks the server afresh, unaffected by the old page's live circuit.
        var afterSignOut = await page.Context.NewPageAsync();
        await afterSignOut.GotoAsync("/dashboard");

        await Expect(afterSignOut).ToHaveURLAsync(new Regex("/login\\?"));
    }

    [Fact]
    public async Task OpensTheUserMenuWithAMouseClick()
    {
        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);

        // Settle the dashboard before clicking, on a signal that does not assume any reports
        // exist — see PagerCaption.
        await Expect(PagerCaption(page)).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = AdminDisplayName }).First.ClickAsync();

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Wyloguj" })).ToBeVisibleAsync();
    }
}
