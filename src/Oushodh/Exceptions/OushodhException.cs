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
