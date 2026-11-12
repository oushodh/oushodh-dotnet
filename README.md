# Oushodh for .NET

The official .NET SDK for the [Oushodh](https://oushodh.dev) medicine data API. Oushodh gives you medicine data for
Bangladesh, checked against the DGDA register: brands, generics, strengths, dosage forms, makers, MRP prices,
pictures and the medical text of each medicine.

- .NET 10 (LTS) or .NET 11. Written in C#.
- No dependencies beyond .NET itself: `HttpClient` and `System.Text.Json`, with source-generated JSON, so it is safe
  for trimming and Native AOT.
- Every call is async and takes a `CancellationToken`.
- Nullable reference types throughout.
- Typed models that keep any field the API adds later.
- Retries with backoff, and `Retry-After` honoured on rate limits.

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

## Get an API key

Make an API key on the Keys page at [app.oushodh.dev](https://app.oushodh.dev). A key starts with `oush_` and is
shown once. Keep it on your server: a key inside an app can be copied out of it.

## Quick start

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
```

With no key passed and no `OUSHODH_API_KEY`, the constructor throws an `OushodhException`. Dispose the client when you
are done.

## Methods

| Method                                               | Route                          | Returns                      |
| ---------------------------------------------------- | ------------------------------ | ---------------------------- |
| `GetMetaAsync()`                                     | `GET /v1/meta`                 | `Meta`                       |
| `ListMedicinesAsync(limit, cursor, updatedSince, q)` | `GET /v1/medicines`            | `Page`                       |
| `ListAllMedicinesAsync(limit, updatedSince, q)`      | `GET /v1/medicines`, each page | `IAsyncEnumerable<Medicine>` |
| `SearchAsync(q, limit)`                              | `GET /v1/medicines/search`     | `SearchResult`               |
| `GetMedicineAsync(reference)`                        | `GET /v1/medicines/{ref}`      | `MedicineDetail`             |
| `GetImageUrl(file)`                                  | none                           | the picture's address        |
| `GetImageAsync(file)`                                | `GET /v1/images/{file}`        | the picture's bytes          |

Every async method takes a last `cancellationToken` argument. Cancelling throws `OperationCanceledException`.

### GetMetaAsync

The account the key belongs to. Use it to check that a key works.

```csharp
var meta = await client.GetMetaAsync();
Console.WriteLine($"{meta.Client}, profile {meta.IdProfile}, {meta.Medicines} medicines, imported {meta.LastImport}");
```

### ListMedicinesAsync

One page of medicines, in the order of their permanent number. `limit` is 1 to 500 (default 100). `q` keeps only
medicines whose brand, generic or maker starts with those words, or whose ID matches.

```csharp
var page = await client.ListMedicinesAsync(limit: 100, q: "paracetamol");
Console.WriteLine($"{page.Items.Count} of {page.Total}, next: {page.Next}");
```

### ListAllMedicinesAsync

Every medicine, page after page to the end. It takes the same filters as `ListMedicinesAsync`.

```csharp
await foreach (var medicine in client.ListAllMedicinesAsync(limit: 500))
{
    Save(medicine);
}
```

### SearchAsync

The best matches for a person typing a name, best match first. `limit` is 1 to 50 (default 20). There is no cursor.

```csharp
var result = await client.SearchAsync("napa", limit: 10);
Console.WriteLine($"{result.Items.Count} shown, {result.Total} match");
```

### GetMedicineAsync

One medicine, with its monograph. `reference` is the medicine's ID in any profile, an old ID, or its UUID. The answer
always carries the medicine's current ID.

```csharp
var detail = await client.GetMedicineAsync("MED-012933");
Console.WriteLine(detail.Monograph?.Indications);
```

### GetImageUrl and GetImageAsync

`GetImageUrl` builds a picture's address from its file name. `GetImageAsync` downloads the bytes (JPEG) and sends no
key. See [Images](#images).

```csharp
var file = "50d858e0985ecc7f60418aaf0cc5ab587f42c2570a884095a9e8ccacd0f6545c.jpg";
var url = client.GetImageUrl(file);
byte[] jpeg = await client.GetImageAsync(file);
```

## Models

The models are `Medicine`, `MedicineDetail`, `Monograph`, `Prices`, `Page`, `SearchResult` and `Meta`. Property
names are the API's fields in PascalCase: `product_name` is `ProductName`. A value no source gives is `null`. Prices
are `decimal`, in BDT. Fields the API adds later are kept in `AdditionalProperties`.

## Paging

`ListMedicinesAsync()` gives one page: `Items`, `Next` and `Total`. Pass `Next` back as `cursor` for the next page.
`Next` is `null` on the last page.

```csharp
string? cursor = null;

do
{
    var page = await client.ListMedicinesAsync(limit: 500, cursor: cursor);

    foreach (var medicine in page.Items)
    {
        Save(medicine);
    }

    cursor = page.Next;
}
while (cursor is not null);
```

`ListAllMedicinesAsync()` does this loop for you. Search has no cursor. To page every match, use
`ListAllMedicinesAsync(q: "...")`.

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
JSON, `Code` is `null` and `Message` is `HTTP <status>`. An error never holds your key.

| Type                        | Status | `Code`                             | Retried             |
| --------------------------- | ------ | ---------------------------------- | ------------------- |
| `BadRequestException`       | 400    | `bad_request`                      | no                  |
| `UnauthorizedException`     | 401    | `unauthorized`                     | no                  |
| `PermissionDeniedException` | 403    | `plan_expired` or `plan_limit`     | no                  |
| `NotFoundException`         | 404    | `not_found`                        | no                  |
| `RateLimitException`        | 429    | `rate_limited` or `quota_exceeded` | `rate_limited` only |
| `ServerException`           | 5xx    | `internal`                         | yes                 |
| `ApiConnectionException`    | none   | none: the network failed           | yes                 |
| `ApiTimeoutException`       | none   | none: the request timed out        | yes                 |

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
catch (RateLimitException error) when (error.Code == "quota_exceeded")
{
    Console.WriteLine($"This month's requests are used up. Try again in {error.RetryAfter}.");
}
```

### Plan limits

Some plans read only the first medicines, in the order of their permanent number. Lists and search leave out the rest.
A lookup by ID of such a medicine throws `PermissionDeniedException` with `Code` `plan_limit`. `Meta.Medicines` says
how many medicines your key reads.

## Retries and rate limits

Your plan sets how many requests each key may make in a minute (60 a minute on Developer).

- Over the minute's limit, the API answers `429 rate_limited`. The `Retry-After` header says how many seconds to wait.
  The SDK waits that long and tries again.
- When the month's requests are used up, the API answers `429 quota_exceeded` until the next month. The SDK throws a
  `RateLimitException` at once and never waits. Its `RetryAfter` holds the time to the start of next month.
- After a `5xx`, a network error or a timeout, the SDK waits a little longer each time (exponential backoff with
  jitter) and tries again.
- It never retries any other `4xx`: they give the same answer every time.

The SDK retries at most `MaxRetries` times (default 2). Every route is a `GET`, so a retry is always safe.

## Configuration

The client takes an `OushodhClientOptions`:

| Option               | Default                   | What it does                                                    |
| -------------------- | ------------------------- | --------------------------------------------------------------- |
| `BaseUrl`            | `https://api.oushodh.dev` | Where the API is.                                               |
| `Timeout`            | 30 seconds                | How long to wait for each request.                              |
| `MaxRetries`         | `2`                       | Retries after a `429 rate_limited`, a `5xx` or a network error. |
| `HttpClient`         | a new `HttpClient`        | Your own `HttpClient`, such as one from `IHttpClientFactory`.   |
| `HttpMessageHandler` | none                      | A handler for a new `HttpClient`, such as a stub in tests.      |

```csharp
using var client = new OushodhClient("oush_...", new OushodhClientOptions
{
    Timeout = TimeSpan.FromSeconds(10),
    MaxRetries = 4,
});
```

The API key comes from the first argument, or from the `OUSHODH_API_KEY` environment variable when you pass none. The
client never disposes an `HttpClient` or a handler you pass in.

### IHttpClientFactory

```csharp
builder.Services.AddHttpClient("oushodh");
builder.Services.AddSingleton(provider => new OushodhClient(
    builder.Configuration["Oushodh:ApiKey"],
    new OushodhClientOptions { HttpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("oushodh") }));
```

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

`GetImageAsync` takes the file name, not the address. Any other address belongs to the source, and it can move or be
removed.

## Develop

```sh
dotnet restore
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

The tests use a stub `HttpMessageHandler`. They never call the API.

The code is in `src/Oushodh`. `OushodhClient.cs` and `OushodhClientOptions.cs` sit at the top. The models are in
`Models/`, the exceptions in `Exceptions/`, and the HTTP transport, retries and error mapping in `Http/`.

## Release

For maintainers. Set the version in `src/Oushodh/Oushodh.csproj` and `CHANGELOG.md`, then push a `v*` tag. The Release
workflow runs the tests and publishes to NuGet with trusted publishing.

## Links

- API docs: [oushodh.dev/docs](https://oushodh.dev/docs)
- API keys: [app.oushodh.dev](https://app.oushodh.dev)

## License

MIT. See [LICENSE](LICENSE).
