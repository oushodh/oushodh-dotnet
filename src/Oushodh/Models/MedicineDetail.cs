namespace Oushodh;

/// <summary>One medicine, with its medical text.</summary>
public class MedicineDetail : Medicine
{
    /// <summary><c>null</c> when no source gives the medicine any text.</summary>
    public Monograph? Monograph { get; init; }
}
