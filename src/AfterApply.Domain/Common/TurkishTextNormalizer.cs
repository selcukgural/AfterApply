namespace AfterApply.Domain.Common;

/// <remarks>
/// .NET's invariant culture does not round-trip the Turkish dotted/dotless i
/// pair (ToUpperInvariant leaves 'ı' unchanged instead of mapping it to 'I'),
/// so "Yazılım" and "YAZILIM" would otherwise normalize to different strings.
/// For a Turkey-first product doing free-text name deduplication, folding all
/// four i-variants together first is more useful than linguistically "correct"
/// casing — it's what makes messy real-world company/job names actually merge.
/// </remarks>
public static class TurkishTextNormalizer
{
    public static string FoldCase(string value)
    {
        return value
            .Replace('İ', 'i')
            .Replace('I', 'i')
            .Replace('ı', 'i')
            .ToLowerInvariant();
    }
}
