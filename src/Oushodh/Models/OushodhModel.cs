using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oushodh;

/// <summary>The base of every model. Fields the API adds later land in <see cref="AdditionalProperties"/>.</summary>
public abstract class OushodhModel
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
