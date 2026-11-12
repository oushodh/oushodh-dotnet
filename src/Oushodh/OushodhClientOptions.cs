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
