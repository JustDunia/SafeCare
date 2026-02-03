namespace SafeCare.Services;

/// <summary>
/// Service for application-level rate limiting, particularly useful for Blazor Server
/// where SignalR connections bypass traditional HTTP rate limiting middleware.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Checks if the action is allowed for the given client identifier.
    /// </summary>
    /// <param name="clientId">Unique identifier for the client (e.g., circuit ID, IP address)</param>
    /// <param name="action">The action being rate limited (e.g., "FormSubmission")</param>
    /// <returns>True if the action is allowed, false if rate limited</returns>
    bool IsAllowed(string clientId, string action);

    /// <summary>
    /// Gets the time remaining until the rate limit resets.
    /// </summary>
    /// <param name="clientId">Unique identifier for the client</param>
    /// <param name="action">The action being rate limited</param>
    /// <returns>Time remaining, or null if not rate limited</returns>
    TimeSpan? GetTimeUntilReset(string clientId, string action);
}
