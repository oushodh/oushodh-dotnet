# Oushodh for .NET

The .NET SDK for the Oushodh medicine data API: medicine data for Bangladesh, checked against the DGDA register.

- .NET 10 (LTS) or .NET 11.
- No dependencies beyond .NET itself: `HttpClient` and `System.Text.Json`, with source-generated JSON, so it is safe
  for trimming and Native AOT.
- Every call is async and takes a `CancellationToken`.
- Nullable reference types throughout.

The full API reference is at [oushodh.dev/docs](https://oushodh.dev/docs).

## Install

Install from GitHub for now. Either add the project to your solution:

```sh
git clone https://github.com/oushodh/oushodh-dotnet.git
dotnet add reference oushodh-dotnet/src/Oushodh/Oushodh.csproj
```

Or build a package and install it from a local folder:

```sh
git clone https://github.com/oushodh/oushodh-dotnet.git
dotnet pack oushodh-dotnet/src/Oushodh -c Release -o ./nupkgs
dotnet add package Oushodh --version 0.1.0 --source ./nupkgs
```

Install from NuGet is coming soon (`dotnet add package Oushodh`).

## Quickstart

Make an API key on the Keys page of the member portal at [app.oushodh.dev](https://app.oushodh.dev). A key starts
with `oush_` and is shown once. Keep it on your server: a key inside an app can be copied out of it.

```csharp
using Oushodh;

using var client = new OushodhClient("oush_..."); // or set OUSHODH_API_KEY and call new OushodhClient()

var meta = await client.GetMetaAsync(); // check that the key works
Console.WriteLine($"{meta.Client} reads {meta.Medicines} medicines");

var result = await client.SearchAsync("napa 500", limit: 5);

foreach (var medicine in result.Items)
{
    Console.WriteLine($"{medicine.Id} {medicine.ProductName} {medicine.Prices.Unit?.Amount}");
}

var detail = await client.GetMedicineAsync("MED-012933");
Console.WriteLine(detail.Monograph?.Indications);
```

The client takes an `OushodhClientOptions`:

| Option               | Default                   | What it does                                                         |
| -------------------- | ------------------------- | -------------------------------------------------------------------- |
| `BaseUrl`            | `https://api.oushodh.dev` | Where the API is.                                                    |
| `Timeout`            | 30 seconds                | How long to wait for each request.                                   |
| `MaxRetries`         | `2`                       | Retries after a `429 rate_limited`, a `5xx` or a network error.      |
| `HttpClient`         | a new `HttpClient`        | Your own `HttpClient`, such as one from `IHttpClientFactory`.        |
| `HttpMessageHandler` | none                      | A handler for a new `HttpClient`, such as a stub in tests.           |

With no key passed and no `OUSHODH_API_KEY`, the constructor throws an `OushodhException`.

Dispose the client when you are done. It never disposes an `HttpClient` or a handler you pass in.

### IHttpClientFactory

```csharp
builder.Services.AddHttpClient("oushodh");
builder.Services.AddSingleton(provider => new OushodhClient(
    builder.Configuration["Oushodh:ApiKey"],
    new OushodhClientOptions { HttpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("oushodh") }));
```

## Methods

| Method                                                        | Route                          | Returns                      |
| ------------------------------------------------------------- | ------------------------------ | ---------------------------- |
| `GetMetaAsync()`                                              | `GET /v1/meta`                 | `Meta`                       |
| `ListMedicinesAsync(limit, cursor, updatedSince, q)`          | `GET /v1/medicines`            | `Page`                       |
| `ListAllMedicinesAsync(limit, updatedSince, q)`               | `GET /v1/medicines`, each page | `IAsyncEnumerable<Medicine>` |
| `SearchAsync(q, limit)`                                       | `GET /v1/medicines/search`     | `SearchResult`               |
| `GetMedicineAsync(reference)`                                 | `GET /v1/medicines/{ref}`      | `MedicineDetail`             |
| `GetImageUrl(file)`                                           | none                           | the picture's address        |
| `GetImageAsync(file)`                                         | `GET /v1/images/{file}`        | the picture's bytes          |

Every async method takes a last `cancellationToken` argument. Cancelling throws `OperationCanceledException`.

`reference` is a medicine's ID in any profile, an old ID, or its UUID. The answer always carries the medicine's
current ID.

The models are `Medicine`, `MedicineDetail`, `Monograph`, `Prices`, `Page`, `SearchResult` and `Meta`. Property
names are the API's fields in PascalCase: `product_name` is `ProductName`. A value no source gives is `null`. Prices
are `decimal`, in BDT. Fields the API adds later are kept in `AdditionalProperties`.

## Paging

`ListMedicinesAsync()` gives one page: `Items`, `Next` and `Total`. Pass `Next` back as `cursor` for the next page.
`Next` is `null` on the last page. A page holds 1 to 500 medicines (`limit`, default 100).

`ListAllMedicinesAsync()` does this for you, page after page to the end:

```csharp
await foreach (var medicine in client.ListAllMedicinesAsync(limit: 500))
{
    Save(medicine);
}
```

Search has no cursor. To page every match, use `ListAllMedicinesAsync(q: "...")`.

## Sync

Copy the whole database once, then fetch only what changed.

1. The first sync pages through `ListAllMedicinesAsync()` to the end. Keep the `LastUpdated` of the newest medicine.
2. Each later sync passes that time as `updatedSince` and pages to the end. Medicines come oldest change first.

```csharp
DateTimeOffset? since = LoadLastSync(); // null the first time

await foreach (var medicine in client.ListAllMedicinesAsync(limit: 500, updatedSince: since))
{
    Upsert(medicine.Uuid, medicine);

    if (since is null || medicine.LastUpdated > since)
    {
        since = medicine.LastUpdated;
    }
}

StoreLastSync(since);
```

`updatedSince` is a `DateTimeOffset`. The SDK sends it in UTC to the whole second, like `2026-11-08T09:30:00Z`. It
means "at or after", so the medicine that set the time comes back once more. Store each medicine by its `Uuid`: it
never changes. Medicines are never deleted and IDs are never reused.

## Errors

Every error is an `OushodhException`. It has `Status` (the HTTP status), `Code` (the API's error code) and `Message`.
Match on `Code` or on the type, never on `Message`: the message is for people and may change. When the answer is not
JSON, `Code` is `null` and `Message` is `HTTP <status>`.

| Type                        | Status | `Code`                             |
| --------------------------- | ------ | ---------------------------------- |
| `BadRequestException`       | 400    | `bad_request`                      |
| `UnauthorizedException`     | 401    | `unauthorized`                     |
| `PermissionDeniedException` | 403    | `plan_expired` or `plan_limit`     |
| `NotFoundException`         | 404    | `not_found`                        |
| `RateLimitException`        | 429    | `rate_limited` or `quota_exceeded` |
| `ServerException`           | 5xx    | `internal`                         |
| `ApiConnectionException`    | none   | none: the network failed           |
| `ApiTimeoutException`       | none   | none: the request timed out        |

```csharp
try
{
    var medicine = await client.GetMedicineAsync("MED-012933");
}
catch (NotFoundException)
{
    // no medicine has that ID
}
catch (PermissionDeniedException error) when (error.Code == "plan_limit")
{
    Console.WriteLine("Upgrade your plan to read this medicine.");
}
```

The SDK retries a `429 rate_limited`, a `5xx` and a network error or timeout up to `MaxRetries` times. After a `5xx`
or a network error it waits a little longer each time. It never retries any other `4xx`: they give the same answer
every time. Every route is a `GET`, so a retry is always safe. An error never holds your key.

## Rate limits

Your plan sets how many requests each key may make in a minute (60 a minute on Developer), and how many your account
may make in a month.

- Over the minute's limit, the API answers `429 rate_limited`. The `Retry-After` header says how many seconds to wait.
  The SDK waits that long and tries again.
- When the month's requests are used up, the API answers `429 quota_exceeded` until the next month. The SDK throws a
  `RateLimitException` at once and never waits. Its `RetryAfter` holds the time to the start of next month.

## Plan limits

Some plans read only the first medicines, in the order of their permanent number. Lists and search leave out the rest.
A lookup by ID of such a medicine throws `PermissionDeniedException` with `Code` `plan_limit`. `Meta.Medicines` says
how many medicines your key reads.

## Images

`ProductImage` is the address of a medicine's picture. When it is under `/v1/images/`, Oushodh keeps the copy and
serves it with no key, so an `<img>` tag can show it. The file never changes, so it can be cached for a year.

```csharp
var url = medicine.ProductImage;

if (url is not null && url.StartsWith(client.GetImageUrl(""), StringComparison.Ordinal))
{
    byte[] jpeg = await client.GetImageAsync(url[(url.LastIndexOf('/') + 1)..]);
}
```

`GetImageAsync` takes the file name, not the address, and sends no key. Any other address belongs to the source, and
it can move or be removed.

## Develop

```sh
dotnet restore
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

The tests use a stub `HttpMessageHandler`. They never call the API.

## License

MIT. See [LICENSE](LICENSE).
