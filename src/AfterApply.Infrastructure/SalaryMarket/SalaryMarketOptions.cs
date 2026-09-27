namespace AfterApply.Infrastructure.SalaryMarket;

/// <summary>The public occupation salary pages (/maaslar), built from outside surveys.</summary>
public sealed class SalaryMarketOptions
{
    public const string SectionName = "SalaryMarket";

    /// <summary>Ships dark: the pages go live by an admin switch once they have been walked in
    /// production, like the response-rate table before them.</summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// A cell needs this many answers before it is shown (the user's call on 2026-09-27; 30 was
    /// proposed). The import already leaves smaller cells out of the seed; the service applies
    /// it again when it loads, so a seed built with a lower number cannot publish them. Raising
    /// it is safe, lowering it is a publication decision.
    /// </summary>
    public int MinimumResponses { get; init; } = 15;

    /// <summary>How long a browser or CDN may keep a response. The figures change once a year,
    /// but the flag can switch at runtime, and an hour is how long a switched-off page may still
    /// be served from a shared cache — the response-rate table's number.</summary>
    public int CacheSeconds { get; init; } = 3600;
}
