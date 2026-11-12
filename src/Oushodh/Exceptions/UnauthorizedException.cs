namespace Oushodh;

/// <summary>401 <c>unauthorized</c>: no key, an unknown key or a revoked key.</summary>
public sealed class UnauthorizedException(string message, int status, string? code)
    : OushodhException(message, status, code);
