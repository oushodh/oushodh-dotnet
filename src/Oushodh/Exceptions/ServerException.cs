namespace Oushodh;

/// <summary>5xx, such as <c>internal</c>.</summary>
public sealed class ServerException(string message, int status, string? code)
    : OushodhException(message, status, code);
