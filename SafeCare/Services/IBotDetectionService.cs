namespace SafeCare.Services;

/// <summary>
/// Service for bot detection using honeypot techniques.
/// </summary>
public interface IBotDetectionService
{
    /// <summary>
    /// Validates the honeypot data to detect bots.
    /// </summary>
    /// <param name="honeypotData">The honeypot data collected from the form</param>
    /// <returns>True if the submission appears to be from a human, false if bot detected</returns>
    bool ValidateSubmission(HoneypotData honeypotData);
}

/// <summary>
/// Data collected from honeypot fields for bot detection.
/// </summary>
public sealed record HoneypotData
{
    /// <summary>
    /// Timestamp when the form was loaded (for time-based detection).
    /// </summary>
    public DateTime FormLoadedAt { get; init; }

    /// <summary>
    /// Honeypot field disguised as a secondary email field.
    /// </summary>
    public string? Email2 { get; init; }

    /// <summary>
    /// Honeypot field disguised as a website/URL field.
    /// </summary>
    public string? Website { get; init; }

    /// <summary>
    /// Honeypot field disguised as an address field.
    /// </summary>
    public string? Address { get; init; }
}
