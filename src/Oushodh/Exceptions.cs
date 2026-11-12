namespace Oushodh;

/// <summary>
/// The base of every error the SDK throws. <see cref="Status"/> is the HTTP status and <see cref="Code"/> the API's
/// error code; both are <c>null</c> when no answer came back. Match on <see cref="Code"/> or the type, never on
/// <see cref="Exception.Message"/>.
/// </summary>
public class OushodhException(string message, int? status = null, string? code = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int? Status { get; } = status;

    public string? Code { get; } = code;
}

/// <summary>400 <c>bad_request</c>.</summary>
public sealed class BadRequestException(string message, int status, string? code)
    : OushodhException(message, status, code);

/// <summary>401 <c>unauthorized</c>: no key, an unknown key or a revoked key.</summary>
public sealed class UnauthorizedException(string message, int status, string? code)
    : OushodhException(message, status, code);

/// <summary>403 <c>plan_expired</c> or <c>plan_limit</c>.</summary>
public sealed class PermissionDeniedException(string message, int status, string? code)
    : OushodhException(message, status, code);

/// <summary>404 <c>not_found</c>.</summary>
public sealed class NotFoundException(string message, int status, string? code)
    : OushodhException(message, status, code);

/// <summary>429 <c>rate_limited</c> or <c>quota_exceeded</c>. <see cref="RetryAfter"/> comes from the Retry-After header.</summary>
public sealed class RateLimitException(string message, int status, string? code, TimeSpan? retryAfter)
    : OushodhException(message, status, code)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

/// <summary>5xx, such as <c>internal</c>.</summary>
public sealed class ServerException(string message, int status, string? code)
    : OushodhException(message, status, code);

/// <summary>No answer came back: the network failed.</summary>
public class ApiConnectionException(string message, Exception? innerException = null)
    : OushodhException(message, null, null, innerException);

/// <summary>No answer came back in time.</summary>
public sealed class ApiTimeoutException(string message, Exception? innerException = null)
    : ApiConnectionException(message, innerException);
