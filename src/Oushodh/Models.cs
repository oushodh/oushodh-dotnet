using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oushodh;

/// <summary>The base of every model. Fields the API adds later land in <see cref="AdditionalProperties"/>.</summary>
public abstract class OushodhModel
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>One medicine: one brand in one strength and dosage form. A value no source gives is <c>null</c>.</summary>
public class Medicine : OushodhModel
{
    /// <summary>The medicine's ID in your account's ID profile. It never changes and is never reused.</summary>
    public string Id { get; init; } = "";

    /// <summary>The key that never changes. Store it to match a medicine across syncs.</summary>
    public string Uuid { get; init; } = "";

    public string Brand { get; init; } = "";

    /// <summary>Reserved for a symptom category. <c>null</c> for every medicine today.</summary>
    public string? Category { get; init; }

    /// <summary>Brand, strength and dosage form together.</summary>
    public string ProductName { get; init; } = "";

    /// <summary>The address of the medicine's picture, or <c>null</c> when there is none.</summary>
    public string? ProductImage { get; init; }

    /// <summary>Every picture of the medicine, <see cref="ProductImage"/> first.</summary>
    public IReadOnlyList<string> Images { get; init; } = [];

    /// <summary>The pack's unit, such as <c>Strip</c> or <c>Bottle</c>.</summary>
    public string? Unit { get; init; }

    /// <summary>The pack's size, such as <c>10 Tablets</c> or <c>200 ml</c>.</summary>
    public string? Size { get; init; }

    /// <summary>MRP in BDT of what <see cref="PricePer"/> names. Kept for older clients: read <see cref="Prices"/>.</summary>
    public decimal? Price { get; init; }

    /// <summary>What <see cref="Price"/> buys. Kept for older clients: read <see cref="Prices"/>.</summary>
    public string? PricePer { get; init; }

    /// <summary>Where the price comes from.</summary>
    public string? PriceSource { get; init; }

    /// <summary>The unit price in each pack size. Kept for older clients: read <see cref="Prices"/>.</summary>
    public IReadOnlyList<UnitPrice> UnitPrices { get; init; } = [];

    /// <summary>Every MRP the sources state for the medicine. This is the home of all price data.</summary>
    public Prices Prices { get; init; } = new();

    public string? DescriptionEn { get; init; }

    public string? DescriptionBn { get; init; }

    public string? DosageForm { get; init; }

    public string? Strength { get; init; }

    public string? GenericName { get; init; }

    public string? Manufacturer { get; init; }

    /// <summary>The page the record was read from.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>When any field of this record last changed. Sync uses it.</summary>
    public DateTimeOffset LastUpdated { get; init; }

    /// <summary><c>true</c> when a DGDA registration (DAR number) links to the medicine.</summary>
    public bool DgdaListed { get; init; }

    /// <summary>
    /// The DGDA register that lists it: <c>allopathic</c>, <c>homoeopathic</c>, <c>herbal</c>, <c>ayurvedic</c> or
    /// <c>unani</c>. <c>null</c> when no register lists it.
    /// </summary>
    public string? MedicineType { get; init; }
}

/// <summary>One medicine, with its medical text.</summary>
public class MedicineDetail : Medicine
{
    /// <summary><c>null</c> when no source gives the medicine any text.</summary>
    public Monograph? Monograph { get; init; }
}

/// <summary>The medical text of a medicine, as its source wrote it. Information for professionals, not medical advice.</summary>
public class Monograph : OushodhModel
{
    public string Source { get; init; } = "";

    public string? Indications { get; init; }

    public string? Composition { get; init; }

    public string? Pharmacology { get; init; }

    public string? Dosage { get; init; }

    public string? Interactions { get; init; }

    public string? Contraindications { get; init; }

    public string? SideEffects { get; init; }

    public string? PregnancyLactation { get; init; }

    public string? Precautions { get; init; }

    public string? Overdose { get; init; }

    public string? Storage { get; init; }
}

public class UnitPrice : OushodhModel
{
    /// <summary>The pack size as the source names it, or <c>null</c>.</summary>
    public string? Size { get; init; }

    /// <summary>MRP in BDT of one unit in that size.</summary>
    public decimal Price { get; init; }
}

/// <summary>Every MRP the sources state: per unit, per strip and per pack. No amount is computed from another.</summary>
public class Prices : OushodhModel
{
    /// <summary>Always <c>BDT</c>.</summary>
    public string Currency { get; init; } = "";

    /// <summary>The MRP of one unit. <c>null</c> when no source states it, or when it differs by pack size.</summary>
    public PriceUnit? Unit { get; init; }

    /// <summary>The unit price in each pack size, cheapest first. Empty otherwise.</summary>
    public IReadOnlyList<PriceSize> Sizes { get; init; } = [];

    /// <summary>The MRP of one strip, or <c>null</c>.</summary>
    public PriceStrip? Strip { get; init; }

    /// <summary>The MRP of each pack or box, cheapest first.</summary>
    public IReadOnlyList<PricePack> Packs { get; init; } = [];
}

public class PriceUnit : OushodhModel
{
    public decimal Amount { get; init; }

    public string Source { get; init; } = "";
}

public class PriceSize : OushodhModel
{
    public string? Size { get; init; }

    public decimal Amount { get; init; }

    public string Source { get; init; } = "";
}

public class PriceStrip : OushodhModel
{
    public decimal Amount { get; init; }

    /// <summary>How many units the strip holds, or <c>null</c>.</summary>
    public double? Units { get; init; }

    public string Source { get; init; } = "";
}

public class PricePack : OushodhModel
{
    public decimal Amount { get; init; }

    /// <summary>How many units the pack holds, or <c>null</c>.</summary>
    public double? Units { get; init; }

    public string Label { get; init; } = "";

    public string Source { get; init; } = "";
}

/// <summary>One page of medicines.</summary>
public class Page : OushodhModel
{
    public IReadOnlyList<Medicine> Items { get; init; } = [];

    /// <summary>Pass it back as <c>cursor</c> for the next page. <c>null</c> on the last page.</summary>
    public string? Next { get; init; }

    /// <summary>How many medicines match, across all pages.</summary>
    public int Total { get; init; }
}

/// <summary>The best matches, best first, and the count of every match.</summary>
public class SearchResult : OushodhModel
{
    public IReadOnlyList<Medicine> Items { get; init; } = [];

    /// <summary>How many medicines match, including those past <c>limit</c>.</summary>
    public int Total { get; init; }
}

/// <summary>The account the key belongs to.</summary>
public class Meta : OushodhModel
{
    public string Client { get; init; } = "";

    /// <summary>The ID profile your medicine IDs come from.</summary>
    public string IdProfile { get; init; } = "";

    /// <summary>How many medicines your key reads.</summary>
    public int Medicines { get; init; }

    /// <summary>When the data was last imported.</summary>
    public DateTimeOffset? LastImport { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(Meta))]
[JsonSerializable(typeof(Page))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(MedicineDetail))]
internal sealed partial class OushodhJsonContext : JsonSerializerContext;
