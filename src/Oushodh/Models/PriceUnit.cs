namespace Oushodh;

public class PriceUnit : OushodhModel
{
    public decimal Amount { get; init; }

    public string Source { get; init; } = "";
}
