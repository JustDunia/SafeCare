using Microsoft.Playwright;

namespace SafeCare.E2ETests.Infrastructure;

[Collection(AppCollection.Name)]
public abstract class E2ETestBase(AppFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// The public form refuses anything submitted sooner than this after loading — it is a
    /// bot defence, not a delay to optimise away. Tests must wait it out.
    /// </summary>
    protected static readonly TimeSpan MinimumFormFillTime = TimeSpan.FromSeconds(6);

    private readonly List<IBrowserContext> _tracedContexts = [];

    protected AppFixture Fixture { get; } = fixture;

    /// <summary>
    /// Opens a fresh browser context and page, with Playwright tracing (screenshots +
    /// DOM snapshots + sources) running on the context from the start. The trace is exported
    /// when this test instance is disposed - see <see cref="DisposeAsync"/>.
    ///
    /// Tracing runs unconditionally for every context rather than only on failure: xUnit v3
    /// gives a test class no clean way to learn its own outcome from inside
    /// <c>IAsyncLifetime.DisposeAsync</c>, so "only trace failures" isn't something this
    /// fixture can do without guesswork. The cost is bounded on the CI side instead - the
    /// workflow's "Upload Playwright traces" step only runs `if: failure()`, so a green E2E
    /// job never uploads anything; only a failing run's traces (for every test that ran in it,
    /// not just the failing one) become an artifact. See .github/workflows/ci.yml.
    /// </summary>
    protected async Task<IPage> NewPageAsync()
    {
        var context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Fixture.BaseUrl,
            IgnoreHTTPSErrors = true
        });

        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        _tracedContexts.Add(context);

        return await context.NewPageAsync();
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Stops tracing for every context this test opened and exports each one to
    /// <c>playwright-traces/</c> under the test binaries - the exact directory
    /// .github/workflows/ci.yml uploads from. Each file name is suffixed with a fresh GUID so
    /// that concurrent tests, and a single test that calls <see cref="NewPageAsync"/> more than
    /// once, never overwrite one another's trace.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_tracedContexts.Count == 0)
        {
            return;
        }

        var tracesDir = Path.Combine(AppContext.BaseDirectory, "playwright-traces");
        Directory.CreateDirectory(tracesDir);

        foreach (var context in _tracedContexts)
        {
            var tracePath = Path.Combine(tracesDir, $"{GetType().Name}-{Guid.NewGuid():N}.zip");
            await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
            await context.CloseAsync();
        }
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
        await SubmitLoginFormAsync(page, "admin", "Admin123!");
        await page.WaitForURLAsync(url => !url.Contains("/login"));
    }

    /// <summary>
    /// Fills the login form that is already open and submits it, without waiting for the
    /// outcome — callers decide what to expect (a redirect away, or an error on /login).
    ///
    /// The form is prerendered and only becomes interactive once the SignalR circuit connects,
    /// which happens after <c>GotoAsync</c> has already returned. Typing before that moment
    /// changes the DOM but never reaches the server-side model, so the hidden inputs the POST
    /// actually reads stay empty and a plain "fill, then wait" hangs until its timeout — this
    /// flaked roughly one run in three. Filling is therefore retried until the hidden inputs
    /// confirm the circuit picked the values up.
    /// </summary>
    protected static async Task SubmitLoginFormAsync(IPage page, string userName, string password)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (true)
        {
            await page.FillAsync("input[autocomplete='username']", userName);
            await page.FillAsync("input[autocomplete='current-password']", password);

            try
            {
                await page.WaitForFunctionAsync(
                    """
                    ([user, pass]) =>
                        document.querySelector('input[name="userName"]')?.value === user
                        && document.querySelector('input[name="password"]')?.value === pass
                    """,
                    new[] { userName, password },
                    new PageWaitForFunctionOptions { Timeout = 2_000 });
                break;
            }
            catch (TimeoutException) when (DateTime.UtcNow < deadline)
            {
                // circuit not connected yet - type again
            }
        }

        await page.ClickAsync("form button[type='submit']");
    }
}
