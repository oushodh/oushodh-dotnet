namespace Oushodh;

/// <summary>403 <c>plan_expired</c> or <c>plan_limit</c>.</summary>
public sealed class PermissionDeniedException(string message, int status, string? code)
    : OushodhException(message, status, code);
