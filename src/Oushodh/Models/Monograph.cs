namespace Oushodh;

/// <summary>The medical text of a medicine, as its source wrote it. Information for professionals, not medical advice.</summary>
public class Monograph : OushodhModel
{
    public string Source { get; init; } = "";

    public string? Indications { get; init; }

    public string? Composition { get; init; }

    public string? Pharmacology { get; init; }

    public string? Dosage { get; init; }

    public string? Interactions { get; init; }

    public string? Contraindications { get; init; }

    public string? SideEffects { get; init; }

    public string? PregnancyLactation { get; init; }

    public string? Precautions { get; init; }

    public string? Overdose { get; init; }

    public string? Storage { get; init; }
}
