namespace AfterApply.Domain.Occupations;

/// <summary>Where a catalogue row comes from: an ISCO-08 unit group, or a market title we added
/// because the unit group was too coarse for salary comparison ("Backend Developer" inside 2512).</summary>
public enum OccupationSource
{
    Isco08,
    Curated
}
