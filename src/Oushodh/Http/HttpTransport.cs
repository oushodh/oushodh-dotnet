using System.Net.Http.Headers;

namespace Oushodh;

/// <summary>Sends one GET, retrying as <see cref="RetryPolicy"/> says, and turns a failed answer into an exception.</summary>
internal sealed class HttpTransport : IDisposable
{
    private static readonly string UserAgent =
        $"oushodh-dotnet/{typeof(OushodhClient).Assembly.GetName().Version?.ToString(3)}";

    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly TimeSpan _timeout;
    private readonly int _maxRetries;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public HttpTransport(string apiKey, string baseUrl, OushodhClientOptions options)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl;
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

    public Func<TimeSpan, CancellationToken, Task> Sleep { get; set; } = Task.Delay;

    public async Task<byte[]> SendAsync(string path, bool auth, CancellationToken cancellationToken)
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

                    error = ApiErrors.FromResponse(response, body);
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

            var delay = RetryPolicy.Delay(error, attempt, _maxRetries);

            if (delay is null)
            {
                throw error;
            }

            await Sleep(delay.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
