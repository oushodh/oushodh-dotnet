namespace Oushodh;

/// <summary>No answer came back: the network failed.</summary>
public class ApiConnectionException(string message, Exception? innerException = null)
    : OushodhException(message, null, null, innerException);
