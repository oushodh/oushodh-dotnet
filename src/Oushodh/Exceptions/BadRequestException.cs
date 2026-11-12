namespace Oushodh;

/// <summary>400 <c>bad_request</c>.</summary>
public sealed class BadRequestException(string message, int status, string? code)
    : OushodhException(message, status, code);
