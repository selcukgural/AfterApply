namespace AfterApply.Infrastructure.Companies;

public sealed class CompanySearchOptions
{
    // Trigram similarity (0-1) a scraped extension company name must clear against an existing
    // Company.NormalizedName to be silently auto-attached instead of creating a new row.
    // Empirically calibrated (real pg_trgm similarity() against realistic company-name pairs,
    // not guessed): genuine typo/near-duplicate pairs cluster at 0.58-0.80 (e.g. "Nova Yazilim"
    // vs "Nova Yazlim" = 0.667, "Trendyol Group" vs "Trendyol Grup" = 0.706), while genuinely
    // different companies land at 0.15-0.38 even when they share a word (e.g. "Nova Teknoloji"
    // vs "Nova Yazilim" = 0.217). 0.75 (the original guess before measuring) misses most real
    // typos; 0.5 keeps a solid margin below the near-duplicate cluster's floor.
    public double FuzzyMatchThreshold { get; init; } = 0.5;

    // Autocomplete queries shorter than this return an empty list (both here and, defense in
    // depth, client-side before a request is even fired).
    public int MinQueryLength { get; init; } = 2;

    public int MaxResults { get; init; } = 8;
}
