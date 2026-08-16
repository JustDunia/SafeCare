using Microsoft.Playwright;

namespace SafeCare.E2ETests.Infrastructure;

[Collection(AppCollection.Name)]
public abstract class E2ETestBase(AppFixture fixture)
{
    /// <summary>
    /// The public form refuses anything submitted sooner than this after loading — it is a
    /// bot defence, not a delay to optimise away. Tests must wait it out.
    /// </summary>
    protected static readonly TimeSpan MinimumFormFillTime = TimeSpan.FromSeconds(6);

    protected AppFixture Fixture { get; } = fixture;

    protected async Task<IPage> NewPageAsync()
    {
        var context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Fixture.BaseUrl,
            IgnoreHTTPSErrors = true
        });

        return await context.NewPageAsync();
    }

    /// <summary>
    /// Logs in as the seeded administrator.
    ///
    /// Login.razor's visible fields are MudBlazor text fields, not plain HTML inputs with a
    /// `name` attribute — Playwright has to target them by their `autocomplete` attribute
    /// instead. The actual POST to /signin reads from separate hidden `&lt;input name="userName"&gt;`
    /// / `name="password"` fields that only pick up the typed value once Blazor Server's
    /// SignalR round trip re-renders them, so this waits for that sync before submitting.
    /// The submit button selector is scoped to `form` because the page also contains two more
    /// `button[type='submit']` elements belonging to Blazor's reconnect/resume UI.
    /// </summary>
    protected async Task LoginAsAdminAsync(IPage page)
    {
        await page.GotoAsync("/login");
        await page.FillAsync("input[autocomplete='username']", "admin");
        await page.FillAsync("input[autocomplete='current-password']", "Admin123!");

        await page.WaitForFunctionAsync("""
            () => document.querySelector('input[name="userName"]')?.value === 'admin'
                && document.querySelector('input[name="password"]')?.value === 'Admin123!'
            """);

        await page.ClickAsync("form button[type='submit']");
        await page.WaitForURLAsync(url => !url.Contains("/login"));
    }
}
