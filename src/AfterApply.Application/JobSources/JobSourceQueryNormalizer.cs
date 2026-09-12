using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AfterApply.Domain.Common;
using AfterApply.Domain.JobSources;

namespace AfterApply.Application.JobSources;

/// <summary>
/// Turns what a user typed into the shared query it belongs to. Normalisation is deliberately
/// mild — collapse whitespace, lower-case with the invariant culture, keep diacritics — because
/// the source is case-insensitive but "İstanbul" and "Istanbul" are different strings to it, and
/// the point is to send the source what a person would.
/// </summary>
public static partial class JobSourceQueryNormalizer
{
    public static string NormalizeText(string value) => CollapseWhitespace(value).ToLowerInvariant();

    /// <summary>Trim and collapse runs of whitespace; case and diacritics untouched. What is stored
    /// on the profile and what the user sees echoed back.</summary>
    public static string CollapseWhitespace(string value) => WhitespaceRegex().Replace(value.Trim(), " ");

    public static string KeyHash(Source source, string keywords, string location, JobSourceTimeWindow window, bool remoteOnly)
    {
        var canonical = string.Join('\n', source.ToString(), NormalizeText(keywords), NormalizeText(location),
            ((int)window).ToString(System.Globalization.CultureInfo.InvariantCulture), remoteOnly ? "1" : "0");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
