namespace Oushodh;

/// <summary>One page of medicines.</summary>
public class Page : OushodhModel
{
    public IReadOnlyList<Medicine> Items { get; init; } = [];

    /// <summary>Pass it back as <c>cursor</c> for the next page. <c>null</c> on the last page.</summary>
    public string? Next { get; init; }

    /// <summary>How many medicines match, across all pages.</summary>
    public int Total { get; init; }
}
