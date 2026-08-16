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
    public void RejectsSubmissionFasterThanMinimumFillTime()
    {
        var data = HumanLikeData() with { FormLoadedAt = DateTime.UtcNow.AddSeconds(-1) };

        Assert.False(CreateSut().ValidateSubmission(data));
    }

    [Fact]
    public void AcceptsSubmissionExactlyAtTheFillTimeBoundary()
    {
        var data = HumanLikeData() with { FormLoadedAt = DateTime.UtcNow.AddSeconds(-6) };

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
