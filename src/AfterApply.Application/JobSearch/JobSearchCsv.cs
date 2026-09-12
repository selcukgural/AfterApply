namespace AfterApply.Application.JobSearch;

/// <summary>
/// The comma-separated query parameters (<c>employmentTypes</c>, <c>jobRequirements</c>,
/// <c>excludeJobPublishers</c>, <c>ids</c>) as lists: trimmed, empties dropped, duplicates removed
/// case-insensitively, first occurrence's order kept. Pure, so the validators and the service
/// agree on exactly what a value means.
/// </summary>
public static class JobSearchCsv
{
    /// <param name="ignoreCase">Off for provider job ids, which are base64 and case-sensitive.</param>
    public static IReadOnlyList<string> Split(string? value, bool ignoreCase = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var seen = new HashSet<string>(ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var raw in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (seen.Add(raw))
            {
                result.Add(raw);
            }
        }

        return result;
    }

    /// <summary>Parses a list of enum member names (case-insensitive). False when any token is not
    /// a member; <paramref name="values"/> is then the tokens that did parse, for error text.</summary>
    public static bool TryParseEnumList<TEnum>(string? value, out IReadOnlyList<TEnum> values, out string? firstInvalid)
        where TEnum : struct, Enum
    {
        var parsed = new List<TEnum>();
        firstInvalid = null;
        foreach (var token in Split(value))
        {
            // Numeric input ("3") would also satisfy Enum.TryParse; it is not a name and is refused.
            if (!Enum.TryParse<TEnum>(token, ignoreCase: true, out var member) || !Enum.IsDefined(member)
                || char.IsDigit(token[0]) || token[0] == '-')
            {
                firstInvalid ??= token;
                continue;
            }

            if (!parsed.Contains(member))
            {
                parsed.Add(member);
            }
        }

        values = parsed;
        return firstInvalid is null;
    }

    public static bool IsValidEnumList<TEnum>(string? value) where TEnum : struct, Enum =>
        TryParseEnumList<TEnum>(value, out _, out _);
}
