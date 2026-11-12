using System.Text.Json.Serialization;

namespace Oushodh;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(Meta))]
[JsonSerializable(typeof(Page))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(MedicineDetail))]
internal sealed partial class OushodhJsonContext : JsonSerializerContext;
