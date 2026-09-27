namespace AfterApply.Infrastructure.Board;

public sealed class BoardOptions
{
    public const string SectionName = "Board";

    /// <summary>Whether the board exists: its routes, the view toggle's third option, and the
    /// automatic placement of new applications. Off by default until it has been walked in a
    /// browser (DECISIONS.md 2026-09-27).</summary>
    public bool Enabled { get; init; }

    /// <summary>On the first opening, open applications (and saved postings) touched within this
    /// many days go on the board. Older ones stay in the list until the user adds them.</summary>
    public int SeedWindowDays { get; init; } = 30;

    /// <summary>How long a closed application stays in the board's last column before it drops off
    /// (it stays in the list).</summary>
    public int ClosedVisibleDays { get; init; } = 14;

    /// <summary>"No reply for this long" — the silence filter and the card tones' amber step.</summary>
    public int SilentDays { get; init; } = 14;
}
