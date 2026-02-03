using System.Collections.Concurrent;

namespace SafeCare.Services;

/// <summary>
/// In-memory rate limiting service for Blazor Server applications.
/// Uses a sliding window algorithm to track requests per client.
/// </summary>
public class RateLimitService : IRateLimitService, IDisposable
{
    private readonly ConcurrentDictionary<string, RateLimitEntry> _entries = new();
    private readonly Timer _cleanupTimer;
    private readonly ILogger<RateLimitService> _logger;

    // Configuration: 5 submissions per 1 minute window
    private const int MaxRequests = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    public RateLimitService(ILogger<RateLimitService> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, CleanupInterval, CleanupInterval);
    }

    public bool IsAllowed(string clientId, string action)
    {
        var key = $"{clientId}:{action}";
        var now = DateTime.UtcNow;

        var entry = _entries.AddOrUpdate(
            key,
            _ => new RateLimitEntry(now, 1),
            (_, existing) =>
            {
                if (now - existing.WindowStart >= Window)
                {
                    // Window has expired, reset
                    return new RateLimitEntry(now, 1);
                }

                // Increment count within window
                return existing with { Count = existing.Count + 1 };
            });

        var isAllowed = entry.Count <= MaxRequests;

        if (!isAllowed)
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientId} on action {Action}. Count: {Count}",
                clientId, action, entry.Count);
        }

        return isAllowed;
    }

    public TimeSpan? GetTimeUntilReset(string clientId, string action)
    {
        var key = $"{clientId}:{action}";

        if (_entries.TryGetValue(key, out var entry))
        {
            var elapsed = DateTime.UtcNow - entry.WindowStart;
            if (elapsed < Window && entry.Count >= MaxRequests)
            {
                return Window - elapsed;
            }
        }

        return null;
    }

    private void CleanupExpiredEntries(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _entries
            .Where(kvp => now - kvp.Value.WindowStart >= Window * 2)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _entries.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired rate limit entries", expiredKeys.Count);
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }

    private sealed record RateLimitEntry(DateTime WindowStart, int Count);
}
