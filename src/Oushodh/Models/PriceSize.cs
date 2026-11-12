namespace Oushodh;

public class PriceSize : OushodhModel
{
    public string? Size { get; init; }

    public decimal Amount { get; init; }

    public string Source { get; init; } = "";
}
