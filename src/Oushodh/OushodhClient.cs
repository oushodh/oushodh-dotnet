using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Oushodh;

/// <summary>The Oushodh API client. Every method is a GET, so every retry is safe.</summary>
public sealed class OushodhClient : IDisposable
{
    private readonly string _baseUrl;
    private readonly HttpTransport _transport;

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

        _baseUrl = options.BaseUrl.ToString().TrimEnd('/');
        _transport = new HttpTransport(key, _baseUrl, options);
    }

    internal Func<TimeSpan, CancellationToken, Task> Sleep
    {
        get => _transport.Sleep;
        set => _transport.Sleep = value;
    }

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

        return await _transport.SendAsync("/v1/images/" + Uri.EscapeDataString(file), false, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _transport.Dispose();
    }

    private async Task<T> GetJsonAsync<T>(string path, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        var body = await _transport.SendAsync(path, true, cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize(body, type) ?? throw new OushodhException("The API sent an empty answer");
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
