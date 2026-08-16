using Microsoft.Extensions.Logging.Abstractions;
using SafeCare.Services;

namespace SafeCare.Tests.Unit.Services;

public class RateLimitServiceTests
{
    private const string Action = "submit-report";

    private static RateLimitService CreateSut() => new(NullLogger<RateLimitService>.Instance);

    [Fact]
    public void AllowsUpToTheConfiguredLimit()
    {
        using var sut = CreateSut();
        var client = Guid.NewGuid().ToString();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            Assert.True(sut.IsAllowed(client, Action), $"Attempt {attempt} should be allowed");
        }
    }

    [Fact]
    public void BlocksTheAttemptAfterTheLimit()
    {
        using var sut = CreateSut();
        var client = Guid.NewGuid().ToString();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            sut.IsAllowed(client, Action);
        }

        Assert.False(sut.IsAllowed(client, Action));
    }

    [Fact]
    public void TracksClientsIndependently()
    {
        using var sut = CreateSut();
        var noisyClient = Guid.NewGuid().ToString();
        var quietClient = Guid.NewGuid().ToString();

        for (var attempt = 1; attempt <= 6; attempt++)
        {
            sut.IsAllowed(noisyClient, Action);
        }

        Assert.True(sut.IsAllowed(quietClient, Action));
    }

    [Fact]
    public void TracksActionsIndependently()
    {
        using var sut = CreateSut();
        var client = Guid.NewGuid().ToString();

        for (var attempt = 1; attempt <= 6; attempt++)
        {
            sut.IsAllowed(client, "submit-report");
        }

        Assert.True(sut.IsAllowed(client, "some-other-action"));
    }

    [Fact]
    public void ReportsNoResetDelayBeforeTheLimitIsReached()
    {
        using var sut = CreateSut();
        var client = Guid.NewGuid().ToString();

        sut.IsAllowed(client, Action);

        Assert.Null(sut.GetTimeUntilReset(client, Action));
    }

    [Fact]
    public void ReportsRemainingWindowOnceTheLimitIsReached()
    {
        using var sut = CreateSut();
        var client = Guid.NewGuid().ToString();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            sut.IsAllowed(client, Action);
        }

        var remaining = sut.GetTimeUntilReset(client, Action);

        Assert.NotNull(remaining);
        Assert.InRange(remaining!.Value, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void ReportsNoResetDelayForAnUnknownClient()
    {
        using var sut = CreateSut();

        Assert.Null(sut.GetTimeUntilReset(Guid.NewGuid().ToString(), Action));
    }
}
