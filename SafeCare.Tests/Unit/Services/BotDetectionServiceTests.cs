using Microsoft.Extensions.Logging.Abstractions;
using SafeCare.Services;

namespace SafeCare.Tests.Unit.Services;

public class BotDetectionServiceTests
{
    private static BotDetectionService CreateSut() => new(NullLogger<BotDetectionService>.Instance);

    /// <summary>A form loaded long enough ago to have been filled in by a human.</summary>
    private static HoneypotData HumanLikeData() => new()
    {
        FormLoadedAt = DateTime.UtcNow.AddSeconds(-30),
        Email2 = null,
        Website = null,
        Address = null
    };

    [Fact]
    public void AcceptsSubmissionFilledSlowlyWithEmptyHoneypots()
    {
        var result = CreateSut().ValidateSubmission(HumanLikeData());

        Assert.True(result);
    }

    [Fact]
    public void RejectsSubmissionJustBelowTheFillTimeThreshold()
    {
        // -4.999s put this one millisecond inside the threshold - finer than the clock the
        // service reads. DateTime.UtcNow advances in jumps, measured here at up to 13ms, so
        // by the time ValidateSubmission sampled it more than 5 seconds had elapsed and the
        // submission was accepted: green on Linux CI, red on every Windows run. 100ms clears
        // that granularity by a wide margin and still pins the boundary tightly.
        var data = HumanLikeData() with { FormLoadedAt = DateTime.UtcNow.AddSeconds(-4.9) };

        Assert.False(CreateSut().ValidateSubmission(data));
    }

    [Fact]
    public void AcceptsSubmissionJustPastTheFillTimeThreshold()
    {
        // -6s (the original value) sat nowhere near the 5-second threshold, so paired with
        // the -1s reject case it only pinned the boundary to the open interval (1s, 6s] -
        // it could drift to, say, 2s or 6s and neither test would notice. This value sits
        // just past the threshold instead, so the two tests together pin it tightly. One
        // millisecond is safe on this side, unlike the reject case above: the clock only
        // ever moves this submission further past the threshold, never back inside it.
        var data = HumanLikeData() with { FormLoadedAt = DateTime.UtcNow.AddSeconds(-5.001) };

        Assert.True(CreateSut().ValidateSubmission(data));
    }

    [Fact]
    public void RejectsWhenEmail2HoneypotIsFilled()
    {
        var data = HumanLikeData() with { Email2 = "bot@example.com" };

        Assert.False(CreateSut().ValidateSubmission(data));
    }

    [Fact]
    public void RejectsWhenWebsiteHoneypotIsFilled()
    {
        var data = HumanLikeData() with { Website = "http://spam.example" };

        Assert.False(CreateSut().ValidateSubmission(data));
    }

    [Fact]
    public void RejectsWhenAddressHoneypotIsFilled()
    {
        var data = HumanLikeData() with { Address = "ul. Spamowa 1" };

        Assert.False(CreateSut().ValidateSubmission(data));
    }
}
