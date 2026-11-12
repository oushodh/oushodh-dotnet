namespace Oushodh;

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
