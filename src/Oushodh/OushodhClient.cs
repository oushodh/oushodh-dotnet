using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Oushodh;

/// <summary>Options for <see cref="OushodhClient"/>.</summary>
public sealed class OushodhClientOptions
{
    /// <summary>Where the API is. Defaults to <c>https://api.oushodh.dev</c>.</summary>
    public Uri BaseUrl { get; set; } = new("https://api.oushodh.dev");

    /// <summary>How long to wait for each request. Defaults to 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Retries after a 429 <c>rate_limited</c>, a 5xx or a network error. Defaults to 2.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Your own <see cref="System.Net.Http.HttpClient"/>, such as one from IHttpClientFactory. The client does not dispose it.</summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>A handler for a new <see cref="System.Net.Http.HttpClient"/>, such as a stub in tests. The client does not dispose it.</summary>
    public HttpMessageHandler? HttpMessageHandler { get; set; }
}

/// <summary>The Oushodh API client. Every method is a GET, so every retry is safe.</summary>
public sealed class OushodhClient : IDisposable
{
    private static readonly string UserAgent =
        $"oushodh-dotnet/{typeof(OushodhClient).Assembly.GetName().Version?.ToString(3)}";

    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly TimeSpan _timeout;
    private readonly int _maxRetries;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    /// <summary>Makes a client. <paramref name="apiKey"/> defaults to the <c>OUSHODH_API_KEY</c> environment variable.</summary>
    /// <exception cref="OushodhException">There is no API key.</exception>
    public OushodhClient(string? apiKey = null, OushodhClientOptions? options = null)
    {
        options ??= new OushodhClientOptions();

        var key = string.IsNullOrEmpty(apiKey) ? Environment.GetEnvironmentVariable("OUSHODH_API_KEY") : apiKey;

        if (string.IsNullOrEmpty(key))
        {
            throw new OushodhException("No API key. Pass apiKey or set the OUSHODH_API_KEY environment variable.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxRetries);

        _apiKey = key;
        _baseUrl = options.BaseUrl.ToString().TrimEnd('/');
        _timeout = options.Timeout;
        _maxRetries = options.MaxRetries;
        _ownsHttp = options.HttpClient is null;
        _http = options.HttpClient
            ?? (options.HttpMessageHandler is null ? new HttpClient() : new HttpClient(options.HttpMessageHandler, false));

        if (_ownsHttp)
        {
            _http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        }
    }

    internal Func<TimeSpan, CancellationToken, Task> Sleep { get; set; } = Task.Delay;

    /// <summary>The account the key belongs to. Use it to check that a key works.</summary>
    public async Task<Meta> GetMetaAsync(CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync("/v1/meta", OushodhJsonContext.Default.Meta, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>One page of medicines, in the order of their permanent number.</summary>
    /// <param name="limit">Medicines on one page, 1 to 500. The API's default is 100.</param>
    /// <param name="cursor">The <see cref="Page.Next"/> of the page before.</param>
    /// <param name="updatedSince">Only medicines changed at or after this time, oldest change first. Sent in UTC, to the second.</param>
    /// <param name="q">Only medicines whose brand, generic or manufacturer starts with these words, or whose ID matches.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<Page> ListMedicinesAsync(
        int? limit = null,
        string? cursor = null,
        DateTimeOffset? updatedSince = null,
        string? q = null,
        CancellationToken cancellationToken = default)
    {
        var since = updatedSince?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var path = "/v1/medicines" + Query(("limit", limit?.ToString(CultureInfo.InvariantCulture)), ("cursor", cursor), ("updated_since", since), ("q", q));

        return await GetJsonAsync(path, OushodhJsonContext.Default.Page, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Every medicine, page after page to the end. It takes the same filters as <see cref="ListMedicinesAsync"/>.</summary>
    public async IAsyncEnumerable<Medicine> ListAllMedicinesAsync(
        int? limit = null,
        DateTimeOffset? updatedSince = null,
        string? q = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? cursor = null;

        do
        {
            var page = await ListMedicinesAsync(limit, cursor, updatedSince, q, cancellationToken).ConfigureAwait(false);

            foreach (var medicine in page.Items)
            {
                yield return medicine;
            }

            cursor = page.Next;
        }
        while (cursor is not null);
    }

    /// <summary>The best matches for a person typing a name, best match first. There is no cursor.</summary>
    /// <param name="q">Brand, generic, manufacturer or alternate-name words, or an ID.</param>
    /// <param name="limit">Most medicines to return, 1 to 50. The API's default is 20.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<SearchResult> SearchAsync(string q, int? limit = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(q);

        var path = "/v1/medicines/search" + Query(("q", q), ("limit", limit?.ToString(CultureInfo.InvariantCulture)));

        return await GetJsonAsync(path, OushodhJsonContext.Default.SearchResult, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>One medicine, with its monograph. The answer always carries the medicine's current ID.</summary>
    /// <param name="reference">The medicine's ID in any profile, an old ID, or its UUID.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<MedicineDetail> GetMedicineAsync(string reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var path = "/v1/medicines/" + Uri.EscapeDataString(reference);

        return await GetJsonAsync(path, OushodhJsonContext.Default.MedicineDetail, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The address of a picture, as <see cref="Medicine.ProductImage"/> gives it. It needs no key.</summary>
    /// <param name="file">The picture's file name: its SHA-256 and <c>.jpg</c>.</param>
    public string GetImageUrl(string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return $"{_baseUrl}/v1/images/{Uri.EscapeDataString(file)}";
    }

    /// <summary>A picture's bytes (JPEG). The request carries no key.</summary>
    /// <param name="file">The picture's file name: its SHA-256 and <c>.jpg</c>. Not a URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<byte[]> GetImageAsync(string file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        return await SendAsync("/v1/images/" + Uri.EscapeDataString(file), false, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private async Task<T> GetJsonAsync<T>(string path, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        var body = await SendAsync(path, true, cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize(body, type) ?? throw new OushodhException("The API sent an empty answer");
    }

    private async Task<byte[]> SendAsync(string path, bool auth, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            OushodhException error;

            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(_timeout);

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + path);

                    request.Headers.UserAgent.ParseAdd(UserAgent);

                    if (auth)
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                        request.Headers.Accept.ParseAdd("application/json");
                    }

                    using var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
                    var body = await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        return body;
                    }

                    error = ApiError(response, body);
                }
                catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                {
                    error = new ApiTimeoutException("The request timed out", exception);
                }
                catch (HttpRequestException exception)
                {
                    error = new ApiConnectionException($"Could not reach the API: {exception.HttpRequestError}", exception);
                }
            }

            var delay = Delay(error, attempt);

            if (delay is null)
            {
                throw error;
            }

            await Sleep(delay.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    private TimeSpan? Delay(OushodhException error, int attempt)
    {
        if (attempt >= _maxRetries)
        {
            return null;
        }

        // quota_exceeded waits until next month, so only rate_limited is retried.
        if (error is RateLimitException rateLimit && rateLimit.Code == "rate_limited")
        {
            return rateLimit.RetryAfter ?? Backoff(attempt);
        }

        if (error is ServerException or ApiConnectionException)
        {
            return Backoff(attempt);
        }

        return null;
    }

    private static TimeSpan Backoff(int attempt)
    {
        var seconds = Math.Min(0.5 * Math.Pow(2, attempt), 8.0) * (0.5 + Random.Shared.NextDouble() / 2);

        return TimeSpan.FromSeconds(seconds);
    }

    private static OushodhException ApiError(HttpResponseMessage response, byte[] body)
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

    private static string Query(params (string Name, string? Value)[] parameters)
    {
        var query = new StringBuilder();

        foreach (var (name, value) in parameters)
        {
            if (value is null)
            {
                continue;
            }

            query.Append(query.Length == 0 ? '?' : '&').Append(name).Append('=').Append(Uri.EscapeDataString(value));
        }

        return query.ToString();
    }
}
