namespace SafeCare.Services;

/// <summary>
/// Bot detection service using multiple honeypot techniques:
/// 1. Hidden form fields with attractive names (bots fill them, humans don't see them)
/// 2. Time-based validation (bots submit too quickly)
/// </summary>
public class BotDetectionService : IBotDetectionService
{
    private readonly ILogger<BotDetectionService> _logger;

    // Minimum time a human would need to fill the form (in seconds)
    private const int MinimumSubmissionTimeSeconds = 5;

    public BotDetectionService(ILogger<BotDetectionService> logger)
    {
        _logger = logger;
    }

    public bool ValidateSubmission(HoneypotData honeypotData)
    {
        // Check 1: Time-based validation - humans need at least a few seconds to fill a form
        var elapsedTime = DateTime.UtcNow - honeypotData.FormLoadedAt;
        if (elapsedTime.TotalSeconds < MinimumSubmissionTimeSeconds)
        {
            _logger.LogWarning(
                "Bot detected: Form submitted too quickly ({ElapsedSeconds:F1}s < {MinSeconds}s minimum)",
                elapsedTime.TotalSeconds,
                MinimumSubmissionTimeSeconds);
            return false;
        }

        // Check 2: Honeypot fields - should all be empty (humans can't see them, bots fill them)
        if (!string.IsNullOrEmpty(honeypotData.Email2))
        {
            _logger.LogWarning("Bot detected: Honeypot field 'email2' was filled");
            return false;
        }

        if (!string.IsNullOrEmpty(honeypotData.Website))
        {
            _logger.LogWarning("Bot detected: Honeypot field 'website' was filled");
            return false;
        }

        if (!string.IsNullOrEmpty(honeypotData.Address))
        {
            _logger.LogWarning("Bot detected: Honeypot field 'address' was filled");
            return false;
        }

        return true;
    }
}
