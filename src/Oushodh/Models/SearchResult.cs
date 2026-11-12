namespace Oushodh;

/// <summary>The best matches, best first, and the count of every match.</summary>
public class SearchResult : OushodhModel
{
    public IReadOnlyList<Medicine> Items { get; init; } = [];

    /// <summary>How many medicines match, including those past <c>limit</c>.</summary>
    public int Total { get; init; }
}
