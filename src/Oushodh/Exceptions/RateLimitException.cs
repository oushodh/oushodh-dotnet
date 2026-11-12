namespace Oushodh;

/// <summary>429 <c>rate_limited</c> or <c>quota_exceeded</c>. <see cref="RetryAfter"/> comes from the Retry-After header.</summary>
public sealed class RateLimitException(string message, int status, string? code, TimeSpan? retryAfter)
    : OushodhException(message, status, code)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
