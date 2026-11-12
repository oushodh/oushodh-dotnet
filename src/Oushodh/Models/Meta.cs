namespace Oushodh;

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
