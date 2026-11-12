namespace Oushodh;

public class PriceStrip : OushodhModel
{
    public decimal Amount { get; init; }

    /// <summary>How many units the strip holds, or <c>null</c>.</summary>
    public double? Units { get; init; }

    public string Source { get; init; } = "";
}
