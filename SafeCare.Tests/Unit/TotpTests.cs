namespace SafeCare.Tests.Unit;

/// <summary>
/// The test helper itself has to be right, or every two-factor test proves nothing.
/// </summary>
public class TotpTests
{
    [Theory]
    // RFC 6238 appendix B, SHA-1 vectors, last six digits. The key is the ASCII string
    // "12345678901234567890" in base32.
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1234567890, "005924")]
    [InlineData(2000000000, "279037")]
    public void MatchesTheRfcTestVectors(long unixSeconds, string expected)
    {
        var code = Totp.Compute("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

        Assert.Equal(expected, code);
    }

    [Fact]
    public void AcceptsTheKeyAsTheSetupPageShowsIt()
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(59);

        Assert.Equal(
            Totp.Compute("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", time),
            Totp.Compute("gezd gnbv gy3t qojq gezd gnbv gy3t qojq", time));
    }
}
