using System.Net;

namespace Oushodh.Tests;

public sealed class ClientTests
{
    private const string Key = "oush_test_123";

    private const string MedicineJson = """
        {
          "id": "MED-012933",
          "uuid": "01a1ba6f-f4d6-7143-ac84-9376f520eef8",
          "brand": "Napa",
          "category": null,
          "product_name": "Napa 500 mg Tablet",
          "product_image": "https://api.example.test/v1/images/50d858e0985ecc7f60418aaf0cc5ab587f42c2570a884095a9e8ccacd0f6545c.jpg",
          "images": ["https://api.example.test/v1/images/50d858e0985ecc7f60418aaf0cc5ab587f42c2570a884095a9e8ccacd0f6545c.jpg"],
          "unit": "Strip",
          "size": "10 Tablets",
          "price": 1.2,
          "price_per": "unit",
          "price_source": "dgda_price",
          "unit_prices": [{ "size": null, "price": 1.2 }],
          "prices": {
            "currency": "BDT",
            "unit": { "amount": 1.2, "source": "dgda_price" },
            "sizes": [{ "size": "15 gm in Tube Pack", "amount": 110.0, "source": "dgda_price" }],
            "strip": { "amount": 12.0, "units": 10, "source": "dgda_price" },
            "packs": [{ "amount": 120.0, "units": null, "label": "100's pack", "source": "dgda_price" }]
          },
          "description_en": null,
          "description_bn": null,
          "dosage_form": "Tablet",
          "strength": "500 mg",
          "generic_name": "Paracetamol",
          "manufacturer": "A Maker",
          "source_url": null,
          "last_updated": "2026-11-09T15:27:34Z",
          "dgda_listed": true,
          "medicine_type": "allopathic"
        }
        """;

    private readonly List<TimeSpan> _sleeps = [];

    private OushodhClient Make(StubHandler handler, int maxRetries = 2, TimeSpan? timeout = null)
    {
        var options = new OushodhClientOptions
        {
            BaseUrl = new Uri("https://api.example.test/"),
            HttpMessageHandler = handler,
            MaxRetries = maxRetries,
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };

        return new OushodhClient(Key, options)
        {
            Sleep = (delay, _) =>
            {
                _sleeps.Add(delay);

                return Task.CompletedTask;
            },
        };
    }

    private static string Error(string code, string message = "Something")
    {
        return $$$"""{"error":{"code":"{{{code}}}","message":"{{{message}}}"}}""";
    }

    private static string PageJson(string next, params string[] ids)
    {
        var items = string.Join(",", ids.Select(id => $$"""{"id":"{{id}}"}"""));

        return $$"""{"items":[{{items}}],"next":{{next}},"total":3}""";
    }

    [Fact]
    public async Task MetaSendsTheKeyAndReadsTheAccount()
    {
        var handler = new StubHandler().Json(
            HttpStatusCode.OK,
            """{"client":"acme","id_profile":"med","medicines":58937,"last_import":"2026-11-09T15:27:40Z","plan":"team"}""");
        using var client = Make(handler);

        var meta = await client.GetMetaAsync(TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);

        Assert.Equal("https://api.example.test/v1/meta", request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(Key, request.Headers.Authorization.Parameter);
        Assert.Equal("oushodh-dotnet/0.1.0", request.Headers.UserAgent.ToString());
        Assert.Equal("acme", meta.Client);
        Assert.Equal("med", meta.IdProfile);
        Assert.Equal(58937, meta.Medicines);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 15, 27, 40, TimeSpan.Zero), meta.LastImport);
        Assert.Equal("team", meta.AdditionalProperties!["plan"].GetString());
    }

    [Fact]
    public async Task ListMedicinesSendsEveryParameter()
    {
        var handler = new StubHandler().Json(HttpStatusCode.OK, PageJson("null"));
        using var client = Make(handler);
        var since = new DateTimeOffset(2026, 11, 8, 15, 30, 0, 999, TimeSpan.FromHours(6));

        await client.ListMedicinesAsync(500, "c3Rh+/=", since, "napa 500", TestContext.Current.CancellationToken);

        Assert.Equal(
            "/v1/medicines?limit=500&cursor=c3Rh%2B%2F%3D&updated_since=2026-11-08T09%3A30%3A00Z&q=napa%20500",
            handler.Requests[0].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task ListMedicinesSendsNoQueryByDefault()
    {
        var handler = new StubHandler().Json(HttpStatusCode.OK, PageJson("null"));
        using var client = Make(handler);

        var page = await client.ListMedicinesAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/medicines", handler.Requests[0].RequestUri!.PathAndQuery);
        Assert.Null(page.Next);
        Assert.Equal(3, page.Total);
    }

    [Fact]
    public async Task ListAllMedicinesPagesToTheEnd()
    {
        var handler = new StubHandler()
            .Json(HttpStatusCode.OK, PageJson("\"two\"", "A", "B"))
            .Json(HttpStatusCode.OK, PageJson("null", "C"));
        using var client = Make(handler);
        var ids = new List<string>();

        await foreach (var medicine in client.ListAllMedicinesAsync(2, q: "napa", cancellationToken: TestContext.Current.CancellationToken))
        {
            ids.Add(medicine.Id);
        }

        Assert.Equal(["A", "B", "C"], ids);
        Assert.Equal("/v1/medicines?limit=2&q=napa", handler.Requests[0].RequestUri!.PathAndQuery);
        Assert.Equal("/v1/medicines?limit=2&cursor=two&q=napa", handler.Requests[1].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task SearchSendsQueryAndLimit()
    {
        var handler = new StubHandler().Json(HttpStatusCode.OK, $$"""{"items":[{{MedicineJson}}],"total":41}""");
        using var client = Make(handler);

        var result = await client.SearchAsync("napa 500", 5, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/medicines/search?q=napa%20500&limit=5", handler.Requests[0].RequestUri!.PathAndQuery);
        Assert.Equal(41, result.Total);
        Assert.Equal("Napa 500 mg Tablet", Assert.Single(result.Items).ProductName);
    }

    [Fact]
    public async Task GetMedicineEscapesTheRefAndReadsEveryField()
    {
        var detail = MedicineJson.TrimEnd().TrimEnd('}')
            + """, "monograph": {"source":"text","indications":"Fever","composition":null,"pharmacology":null,"dosage":null,"interactions":null,"contraindications":null,"side_effects":null,"pregnancy_lactation":"Safe","precautions":null,"overdose":null,"storage":null}, "new_field": 7}""";
        var handler = new StubHandler().Json(HttpStatusCode.OK, detail);
        using var client = Make(handler);

        var medicine = await client.GetMedicineAsync("MED 01/2?x", TestContext.Current.CancellationToken);

        Assert.Equal("/v1/medicines/MED%2001%2F2%3Fx", handler.Requests[0].RequestUri!.PathAndQuery);
        Assert.Equal("MED-012933", medicine.Id);
        Assert.Null(medicine.Category);
        Assert.Equal(1.2m, medicine.Price);
        Assert.Null(Assert.Single(medicine.UnitPrices).Size);
        Assert.Equal("BDT", medicine.Prices.Currency);
        Assert.Equal(1.2m, medicine.Prices.Unit!.Amount);
        Assert.Equal(110.0m, Assert.Single(medicine.Prices.Sizes).Amount);
        Assert.Equal(10, medicine.Prices.Strip!.Units);
        Assert.Null(Assert.Single(medicine.Prices.Packs).Units);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 15, 27, 34, TimeSpan.Zero), medicine.LastUpdated);
        Assert.True(medicine.DgdaListed);
        Assert.Equal("allopathic", medicine.MedicineType);
        Assert.Equal("Fever", medicine.Monograph!.Indications);
        Assert.Equal("Safe", medicine.Monograph.PregnancyLactation);
        Assert.Equal(7, medicine.AdditionalProperties!["new_field"].GetInt32());
    }

    [Fact]
    public async Task GetMedicineReadsANullMonograph()
    {
        var handler = new StubHandler().Json(HttpStatusCode.OK, MedicineJson.TrimEnd().TrimEnd('}') + """, "monograph": null}""");
        using var client = Make(handler);

        var medicine = await client.GetMedicineAsync("MED-012933", TestContext.Current.CancellationToken);

        Assert.Null(medicine.Monograph);
    }

    [Fact]
    public async Task ImagesNeedNoKey()
    {
        const string file = "50d858e0985ecc7f60418aaf0cc5ab587f42c2570a884095a9e8ccacd0f6545c.jpg";
        var handler = new StubHandler().Answer((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0xFF, 0xD8, 0xFF]) }));
        using var client = Make(handler);

        var bytes = await client.GetImageAsync(file, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);

        Assert.Equal([0xFF, 0xD8, 0xFF], bytes);
        Assert.Equal($"/v1/images/{file}", request.RequestUri!.PathAndQuery);
        Assert.Null(request.Headers.Authorization);
        Assert.Equal($"https://api.example.test/v1/images/{file}", client.GetImageUrl(file));
        Assert.Equal("https://api.example.test/v1/images/a%2Fb.jpg", client.GetImageUrl("a/b.jpg"));
    }

    [Fact]
    public async Task AMissingImageIsNotFound()
    {
        var handler = new StubHandler().Json(HttpStatusCode.NotFound, Error("not_found", "No such picture"));
        using var client = Make(handler);

        var error = await Assert.ThrowsAsync<NotFoundException>(() => client.GetImageAsync("x.jpg", TestContext.Current.CancellationToken));

        Assert.Equal("not_found", error.Code);
    }

    [Theory]
    [InlineData(400, "bad_request", typeof(BadRequestException))]
    [InlineData(401, "unauthorized", typeof(UnauthorizedException))]
    [InlineData(403, "plan_expired", typeof(PermissionDeniedException))]
    [InlineData(403, "plan_limit", typeof(PermissionDeniedException))]
    [InlineData(404, "not_found", typeof(NotFoundException))]
    [InlineData(429, "quota_exceeded", typeof(RateLimitException))]
    [InlineData(500, "internal", typeof(ServerException))]
    [InlineData(409, "conflict", typeof(OushodhException))]
    public async Task EachStatusThrowsItsFamily(int status, string code, Type type)
    {
        var handler = new StubHandler().Json((HttpStatusCode)status, Error(code, "For people"));
        using var client = Make(handler, maxRetries: 0);

        var error = await Assert.ThrowsAnyAsync<OushodhException>(() => client.GetMetaAsync(TestContext.Current.CancellationToken));

        Assert.IsType(type, error);
        Assert.Equal(status, error.Status);
        Assert.Equal(code, error.Code);
        Assert.Equal("For people", error.Message);
    }

    [Fact]
    public async Task ABodyThatIsNotJsonHasNoCode()
    {
        var handler = new StubHandler().Answer((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>bad gateway</html>") }));
        using var client = Make(handler, maxRetries: 0);

        var error = await Assert.ThrowsAsync<ServerException>(() => client.GetMetaAsync(TestContext.Current.CancellationToken));

        Assert.Null(error.Code);
        Assert.Equal(502, error.Status);
        Assert.Equal("HTTP 502", error.Message);
    }

    [Fact]
    public async Task RateLimitedWaitsForRetryAfter()
    {
        var handler = new StubHandler()
            .Json(HttpStatusCode.TooManyRequests, Error("rate_limited"), response => response.Headers.Add("Retry-After", "7"))
            .Json(HttpStatusCode.OK, PageJson("null"));
        using var client = Make(handler);

        await client.ListMedicinesAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(7)], _sleeps);
    }

    [Fact]
    public async Task QuotaExceededIsNeverRetried()
    {
        var handler = new StubHandler()
            .Json(HttpStatusCode.TooManyRequests, Error("quota_exceeded"), response => response.Headers.Add("Retry-After", "86400"));
        using var client = Make(handler);

        var error = await Assert.ThrowsAsync<RateLimitException>(() => client.GetMetaAsync(TestContext.Current.CancellationToken));

        Assert.Single(handler.Requests);
        Assert.Empty(_sleeps);
        Assert.Equal("quota_exceeded", error.Code);
        Assert.Equal(TimeSpan.FromDays(1), error.RetryAfter);
    }

    [Fact]
    public async Task AServerErrorIsRetriedThenSucceeds()
    {
        var handler = new StubHandler()
            .Json(HttpStatusCode.InternalServerError, Error("internal"))
            .Json(HttpStatusCode.ServiceUnavailable, "oops")
            .Json(HttpStatusCode.OK, PageJson("null"));
        using var client = Make(handler);

        await client.ListMedicinesAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(2, _sleeps.Count);
        Assert.InRange(_sleeps[0], TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(0.5));
        Assert.InRange(_sleeps[1], TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RetriesStopAtMaxRetries()
    {
        var handler = new StubHandler()
            .Json(HttpStatusCode.InternalServerError, Error("internal"))
            .Json(HttpStatusCode.InternalServerError, Error("internal"))
            .Json(HttpStatusCode.InternalServerError, Error("internal"));
        using var client = Make(handler, maxRetries: 2);

        await Assert.ThrowsAsync<ServerException>(() => client.GetMetaAsync(TestContext.Current.CancellationToken));

        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task NotFoundIsNeverRetried()
    {
        var handler = new StubHandler().Json(HttpStatusCode.NotFound, Error("not_found"));
        using var client = Make(handler);

        await Assert.ThrowsAsync<NotFoundException>(() => client.GetMedicineAsync("MED-0", TestContext.Current.CancellationToken));

        Assert.Single(handler.Requests);
        Assert.Empty(_sleeps);
    }

    [Fact]
    public async Task ANetworkErrorIsRetriedThenThrown()
    {
        var handler = new StubHandler()
            .Answer((_, _) => throw new HttpRequestException(HttpRequestError.NameResolutionError, "No such host"))
            .Answer((_, _) => throw new HttpRequestException(HttpRequestError.ConnectionError, "Refused"));
        using var client = Make(handler, maxRetries: 1);

        var error = await Assert.ThrowsAsync<ApiConnectionException>(() => client.GetMetaAsync(TestContext.Current.CancellationToken));

        Assert.Equal(2, handler.Requests.Count);
        Assert.Null(error.Status);
        Assert.Null(error.Code);
        Assert.Equal("Could not reach the API: ConnectionError", error.Message);
    }

    [Fact]
    public async Task ASlowAnswerTimesOut()
    {
        var handler = new StubHandler().Answer(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = Make(handler, maxRetries: 0, timeout: TimeSpan.FromMilliseconds(50));

        var error = await Assert.ThrowsAsync<ApiTimeoutException>(() => client.GetMetaAsync(TestContext.Current.CancellationToken));

        Assert.Null(error.Status);
    }

    [Fact]
    public async Task CancellingThrowsOperationCanceled()
    {
        using var cancel = new CancellationTokenSource();
        var handler = new StubHandler().Answer(async (_, token) =>
        {
            await cancel.CancelAsync();
            await Task.Delay(TimeSpan.FromSeconds(30), token);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = Make(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetMetaAsync(cancel.Token));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TheKeyNeverAppearsInAnError()
    {
        var handler = new StubHandler()
            .Json(HttpStatusCode.Unauthorized, Error("unauthorized", "A valid API key is required"))
            .Json(HttpStatusCode.TooManyRequests, "not json")
            .Answer((_, _) => throw new HttpRequestException("Refused"));
        using var client = Make(handler, maxRetries: 0);
        var errors = new List<Exception>
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => client.GetMetaAsync(TestContext.Current.CancellationToken)),
            await Assert.ThrowsAsync<RateLimitException>(() => client.GetMetaAsync(TestContext.Current.CancellationToken)),
            await Assert.ThrowsAsync<ApiConnectionException>(() => client.GetMetaAsync(TestContext.Current.CancellationToken)),
        };

        foreach (var error in errors)
        {
            Assert.DoesNotContain(Key, error.ToString());
        }

        Assert.DoesNotContain(Key, client.ToString());
    }
}
