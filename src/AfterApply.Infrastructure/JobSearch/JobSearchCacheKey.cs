using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AfterApply.Domain.Common;
using AfterApply.Domain.JobSearch;

namespace AfterApply.Infrastructure.JobSearch;

/// <summary>
/// One cache key per distinct request. Built from the request's parameters after normalisation
/// (free text case-folded with Turkish rules and whitespace-collapsed, codes lower-cased, lists
/// sorted and de-duplicated), with absent and default-valued parameters left out so "date
/// posted: all" and "date posted unsaid" hash the same. The cursor and page count are part of
/// it: they select a different page of a different size, which is a different answer.
/// </summary>
public static class JobSearchCacheKey
{
    /// <summary>The canonical string the hash is taken over. Exposed for tests and for the
    /// cache row's <c>Parameters</c> column, which stores it for inspection.</summary>
    public static string Canonical(JobSearchOperation operation, IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        var builder = new StringBuilder(operation.ToString());
        foreach (var (name, value) in parameters
                     .Where(p => !string.IsNullOrWhiteSpace(p.Value))
                     .OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            builder.Append('|').Append(name).Append('=').Append(value);
        }

        return builder.ToString();
    }

    public static string Hash(string canonical)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>Free text as it takes part in the key: trimmed, inner whitespace collapsed to one
    /// space, case-folded so that "İstanbul" and "istanbul" are the same search.</summary>
    public static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var collapsed = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return TurkishTextNormalizer.FoldCase(collapsed);
    }

    public static string? NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    public static string? NormalizeList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var items = values.Select(NormalizeText).Where(v => v is not null).Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal).ToArray();
        return items.Length == 0 ? null : string.Join(',', items);
    }

    public static string? Number(int? value) => value?.ToString(CultureInfo.InvariantCulture);
}
