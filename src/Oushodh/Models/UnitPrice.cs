namespace Oushodh;

public class UnitPrice : OushodhModel
{
    /// <summary>The pack size as the source names it, or <c>null</c>.</summary>
    public string? Size { get; init; }

    /// <summary>MRP in BDT of one unit in that size.</summary>
    public decimal Price { get; init; }
}
