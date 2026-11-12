# Changelog

## 0.1.0 (unreleased)

- First version of the .NET SDK for the Oushodh API `/v1`, for .NET 10 and .NET 11.
- `OushodhClient`: `GetMetaAsync()`, `ListMedicinesAsync()`, `ListAllMedicinesAsync()` (an `IAsyncEnumerable` that
  pages to the end), `SearchAsync()`, `GetMedicineAsync()`, `GetImageUrl()` and `GetImageAsync()`.
- Typed models for `Medicine`, `MedicineDetail`, `Monograph`, `Page`, `SearchResult` and `Meta`, with
  source-generated JSON. Unknown fields are kept in `AdditionalProperties`.
- Exceptions by family, each with `Status`, `Code` and `Message`.
- Retries on `429 rate_limited` (honours `Retry-After`), `5xx` and network errors. `429 quota_exceeded` is never
  retried.
