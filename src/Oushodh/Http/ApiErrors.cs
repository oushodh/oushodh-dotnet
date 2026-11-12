using System.Text.Json;

namespace Oushodh;

/// <summary>Turns an answer that is not a 2xx into the exception for its status.</summary>
internal static class ApiErrors
{
    public static OushodhException FromResponse(HttpResponseMessage response, byte[] body)
    {
        var status = (int)response.StatusCode;
        string? code = null;
        var message = $"HTTP {status}";

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var detail)
                && detail.ValueKind == JsonValueKind.Object)
            {
                if (detail.TryGetProperty("code", out var codeValue) && codeValue.ValueKind == JsonValueKind.String)
                {
                    code = codeValue.GetString();
                }

                if (detail.TryGetProperty("message", out var messageValue) && messageValue.ValueKind == JsonValueKind.String)
                {
                    message = messageValue.GetString() ?? message;
                }
            }
        }
        catch (JsonException)
        {
        }

        return status switch
        {
            400 => new BadRequestException(message, status, code),
            401 => new UnauthorizedException(message, status, code),
            403 => new PermissionDeniedException(message, status, code),
            404 => new NotFoundException(message, status, code),
            429 => new RateLimitException(message, status, code, RetryAfter(response)),
            >= 500 => new ServerException(message, status, code),
            _ => new OushodhException(message, status, code),
        };
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;

        if (header?.Delta is { } delta)
        {
            return delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;

            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }
}
