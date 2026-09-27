using System.Globalization;
using System.Text.RegularExpressions;

namespace AfterApply.Application.SalaryMarket.Import;

/// <summary>
/// A salary answer as the surveys ask it: a range ("100.000 - 104.999"), an open top ("15.000 TL
/// ve üzeri") or an open bottom ("2.000 TL ve aşağısı"). <see cref="Value"/> is what one answer
/// counts as in a percentile — the middle of a closed range, the floor of an open top (so a
/// percentile landing there is only "at least"), the middle of an open bottom.
/// </summary>
public sealed record SalaryBand(decimal Value, bool OpenTop)
{
    private static readonly Regex Number = new(@"\d[\d.,]*", RegexOptions.CultureInvariant);

    public static SalaryBand? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var numbers = Number.Matches(text).Select(m => ParseAmount(m.Value)).Where(n => n > 0).ToList();
        if (numbers.Count == 0)
        {
            return null;
        }

        var lower = text.ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        if (lower.Contains("aşağı") || lower.Contains("ve altı"))
        {
            return new SalaryBand(numbers[0] / 2m, false);
        }

        if (lower.Contains("üzeri") || lower.Contains("fazla"))
        {
            return new SalaryBand(numbers[0], true);
        }

        if (numbers.Count >= 2)
        {
            // "100.000 - 104.999" → 102.500: an upper bound ending in 9 is one short of the next
            // range's floor. "5.000 TL - 7.500 TL" (2018) has touching bounds and is taken as written.
            var (low, high) = (numbers[0], numbers[1]);
            var ceiling = high % 10 == 9 ? high + 1 : high;
            return high > low ? new SalaryBand((low + ceiling) / 2m, false) : null;
        }

        // A bare amount (a source that asked for a number, not a range) is the answer itself.
        return new SalaryBand(numbers[0], false);
    }

    // Thousands are written with a dot ("104.999") and nothing in these surveys has decimals.
    private static decimal ParseAmount(string raw) =>
        decimal.TryParse(raw.Replace(".", "").Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
}
