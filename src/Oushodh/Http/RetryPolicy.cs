namespace Oushodh;

/// <summary>How long to wait before the next try, or <c>null</c> to stop.</summary>
internal static class RetryPolicy
{
    public static TimeSpan? Delay(OushodhException error, int attempt, int maxRetries)
    {
        if (attempt >= maxRetries)
        {
            return null;
        }

        // quota_exceeded waits until next month, so only rate_limited is retried.
        if (error is RateLimitException rateLimit && rateLimit.Code == "rate_limited")
        {
            return rateLimit.RetryAfter ?? Backoff(attempt);
        }

        if (error is ServerException or ApiConnectionException)
        {
            return Backoff(attempt);
        }

        return null;
    }

    private static TimeSpan Backoff(int attempt)
    {
        var seconds = Math.Min(0.5 * Math.Pow(2, attempt), 8.0) * (0.5 + Random.Shared.NextDouble() / 2);

        return TimeSpan.FromSeconds(seconds);
    }
}
