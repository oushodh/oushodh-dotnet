namespace Oushodh;

/// <summary>No answer came back in time.</summary>
public sealed class ApiTimeoutException(string message, Exception? innerException = null)
    : ApiConnectionException(message, innerException);
