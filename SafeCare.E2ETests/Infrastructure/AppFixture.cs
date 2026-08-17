using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using SafeCare.Data;
using Testcontainers.PostgreSql;

namespace SafeCare.E2ETests.Infrastructure;

/// <summary>
/// Brings up everything a browser test needs: a database, a mail server, the application
/// itself and a browser. One instance serves the whole test session.
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private readonly IContainer _mailpit = new ContainerBuilder()
        .WithImage("axllent/mailpit:v1.22.0")
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8025))
        .Build();

    // Accumulates the application's stdout and stderr as they are drained asynchronously
    // (see StartApplication). Used both to keep the OS pipe from filling up and blocking the
    // process (RedirectStandardOutput/Error with no reader deadlocks once the 4-64 KB buffer
    // fills) and to surface Serilog's Console-sink output - including Log.Fatal from
    // Program.cs's startup catch block, which never reaches stderr - when startup fails.
    private readonly StringBuilder _appOutput = new();
    private readonly object _appOutputLock = new();

    private Process? _app;
    private IPlaywright? _playwright;
    private bool _disposed;

    public string BaseUrl { get; private set; } = "";

    public string MailPitApiUrl { get; private set; } = "";

    public IBrowser Browser { get; private set; } = null!;

    public DbContextOptions<AppDbContext> DbOptions { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        try
        {
            await Task.WhenAll(_database.StartAsync(), _mailpit.StartAsync());

            DbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_database.GetConnectionString())
                .Options;

            MailPitApiUrl = $"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}";

            var port = FreeTcpPort();
            BaseUrl = $"http://127.0.0.1:{port}";

            StartApplication(port);
            await WaitUntilReadyAsync();

            _playwright = await Playwright.CreateAsync();
            Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }
        catch
        {
            // Either or both containers, and possibly the application process, may already be
            // running by the time any step here throws - e.g. Postgres starts cleanly but
            // MailPit fails, or the app never becomes ready. xUnit is not guaranteed to call
            // DisposeAsync when InitializeAsync throws, so run the same teardown here before
            // rethrowing - otherwise whatever did start leaks on a CI runner. DisposeAsync
            // tears down both containers and the app process unconditionally and is
            // idempotent, so this is safe even if xUnit does end up calling it again
            // afterwards. `throw;` (not `throw ex;`) preserves the original exception -
            // including, for a boot failure, the application's captured stdout/stderr that
            // WaitUntilReadyAsync embeds in its exception message - untouched.
            await DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_app is { HasExited: false })
        {
            _app.Kill(entireProcessTree: true);
            _app.WaitForExit(10_000);
        }

        _app?.Dispose();

        await _mailpit.DisposeAsync();
        await _database.DisposeAsync();
    }

    private static int FreeTcpPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Launches the already-published output of the application (see the class remarks in
    /// the project report for why <c>dotnet run --no-build</c> was replaced with this).
    /// </summary>
    private void StartApplication(int port)
    {
        var projectDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SafeCare"));

        var publishDir = Path.Combine(projectDir, "bin", "Release", "net10.0", "publish");
        var dllPath = Path.Combine(publishDir, "SafeCare.dll");

        if (!File.Exists(dllPath))
        {
            throw new InvalidOperationException(
                $"Published SafeCare output not found at '{dllPath}'. Run " +
                "'dotnet publish SafeCare/SafeCare.csproj -c Release' before running the E2E tests.");
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { "SafeCare.dll" },
            WorkingDirectory = publishDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        // Environment variables win over appsettings files. "Testing" additionally stops
        // appsettings.Development.json from loading at all, so a developer's own database
        // can never be reached from here.
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        startInfo.Environment["ConnectionStrings__DefaultConnection"] = _database.GetConnectionString();
        startInfo.Environment["Email__Provider"] = "Smtp";
        startInfo.Environment["Email__Smtp__Host"] = _mailpit.Hostname;
        startInfo.Environment["Email__Smtp__Port"] = _mailpit.GetMappedPublicPort(1025).ToString();
        startInfo.Environment["Email__Smtp__UseSsl"] = "false";
        startInfo.Environment["Email__Smtp__AuthMode"] = "None";

        _app = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the SafeCare process");

        // Drain stdout/stderr asynchronously via event handlers rather than reading them
        // later with ReadToEndAsync. Serilog's Console sink writes to stdout, and once the OS
        // pipe buffer fills (4 KB on Windows, 64 KB on Linux) an unread pipe makes the child
        // process block on its next write - hanging the whole E2E run until the job timeout.
        // Accumulating both streams here also means WaitUntilReadyAsync has something to show
        // on a boot failure: Program.cs's startup catch reports fatal errors via Log.Fatal,
        // which goes to the Console sink (stdout), so stderr alone is normally empty.
        _app.OutputDataReceived += (_, e) => AppendAppOutput(e.Data);
        _app.ErrorDataReceived += (_, e) => AppendAppOutput(e.Data);
        _app.BeginOutputReadLine();
        _app.BeginErrorReadLine();
    }

    private void AppendAppOutput(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (_appOutputLock)
        {
            _appOutput.AppendLine(line);
        }
    }

    private string CapturedAppOutput()
    {
        lock (_appOutputLock)
        {
            return _appOutput.ToString();
        }
    }

    /// <summary>
    /// Polls the home page until the application answers. Startup includes applying
    /// migrations and seeding, so the first response can take a while.
    /// </summary>
    private async Task WaitUntilReadyAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMinutes(2);

        while (DateTime.UtcNow < deadline)
        {
            if (_app is { HasExited: true })
            {
                throw new InvalidOperationException(
                    $"SafeCare exited during startup (exit code {_app.ExitCode}): {CapturedAppOutput()}");
            }

            try
            {
                var response = await client.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // not listening yet
            }
            catch (TaskCanceledException)
            {
                // the 5-second HttpClient timeout tripped on a slow first response - keep
                // retrying instead of aborting the whole fixture.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"SafeCare did not become ready at {BaseUrl}. Captured output: {CapturedAppOutput()}");
    }
}

[CollectionDefinition(AppCollection.Name)]
public sealed class AppCollection : ICollectionFixture<AppFixture>
{
    public const string Name = "app";
}
