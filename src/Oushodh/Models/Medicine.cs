namespace Oushodh;

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
