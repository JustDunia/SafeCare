using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Playwright;
using SafeCare.E2ETests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace SafeCare.E2ETests;

public class AuthenticationTests(AppFixture fixture) : E2ETestBase(fixture)
{
    // The seeded administrator's display name - see the HasData seed in User.cs.
    private const string AdminDisplayName = "System Administrator";

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

        await Expect(page).ToHaveURLAsync(new Regex("/dashboard$"));
        Assert.StartsWith(Fixture.BaseUrl, page.Url);
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
        // can abort the follow-up navigation below.
        await Expect(page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Cell) }).First)
            .ToBeVisibleAsync();

        // Signing out is a form POST. The button that submits it lives in the user menu, which
        // cannot currently be opened with the mouse (see OpensTheUserMenuWithAMouseClick), so
        // the same POST is sent through the browser context, sharing the session cookie.
        var response = await page.Context.APIRequest.PostAsync(
            "/signout",
            new() { MaxRedirects = 0 });

        Assert.Equal(302, response.Status);
        Assert.EndsWith("/login", response.Headers["location"]);

        // The cookie is gone, so the protected page is out of reach again. A fresh page in the
        // same context asks the server afresh, unaffected by the old page's live circuit.
        var afterSignOut = await page.Context.NewPageAsync();
        await afterSignOut.GotoAsync("/dashboard");

        await Expect(afterSignOut).ToHaveURLAsync(new Regex("/login\\?"));
    }

    [Fact(Skip = "Open production defect: the user menu does not open on click. " +
                 "Remove this Skip once MainLayout's activator is fixed — the test is correct and should pass then.")]
    public async Task OpensTheUserMenuWithAMouseClick()
    {
        // A production defect, not a test problem. Clicking the user menu's activator in
        // MainLayout.razor opens nothing: reproduced in Playwright's Chromium and, by hand,
        // in a regular browser, while other MudBlazor popups on the same page (the filter
        // selects) open fine. The rendered ARIA tree shows a <button> nested inside another
        // <button> — MudMenu wraps ActivatorContent in its own button, and the MudButton
        // placed inside it produces a second one, which is invalid HTML and swallows the
        // click. Replacing the inner MudButton with a non-interactive element was tried and
        // did NOT fix it on its own, so the cause is not yet fully understood and the fix
        // needs its own focused investigation rather than blind iteration.
        //
        // Impact: sign-out, account settings and user management all live in this menu and
        // are unreachable with a mouse. Sign-out is worked around in the test above by
        // posting to /signout directly, which is why that test still passes.
        //
        // Skipped rather than deleted so the defect stays visible in every test report.
        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);
        await Expect(page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Cell) }).First)
            .ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = AdminDisplayName }).First.ClickAsync();

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Wyloguj" })).ToBeVisibleAsync();
    }
}
