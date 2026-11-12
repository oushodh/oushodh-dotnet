namespace Oushodh;

public class PricePack : OushodhModel
{
    public decimal Amount { get; init; }

    /// <summary>How many units the pack holds, or <c>null</c>.</summary>
    public double? Units { get; init; }

    public string Label { get; init; } = "";

    public string Source { get; init; } = "";
}
