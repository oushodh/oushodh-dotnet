namespace Oushodh;

/// <summary>404 <c>not_found</c>.</summary>
public sealed class NotFoundException(string message, int status, string? code)
    : OushodhException(message, status, code);
