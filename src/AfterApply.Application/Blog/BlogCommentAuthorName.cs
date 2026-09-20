using System.Globalization;

namespace AfterApply.Application.Blog;

/// <summary>
/// How a commenter is named on the page (2026-09-20): the first name and the last name's initial
/// — "Selin Y." — enough to tell two readers apart, not enough to find one. Null when the account
/// has no first name (sign-up stopped asking for one on 2026-09-14); the page then says "a
/// reader". Never the e-mail address or any part of it.
/// </summary>
public static class BlogCommentAuthorName
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static string? Format(string? firstName, string? lastName)
    {
        var first = (firstName ?? string.Empty).Trim();
        if (first.Length == 0)
        {
            return null;
        }

        var last = (lastName ?? string.Empty).Trim();
        if (last.Length == 0)
        {
            return first;
        }

        // A last name typed in lower case is capitalised the Turkish way — most readers are
        // Turkish, and "ipek" must become "İ.", not "I."; one already capitalised is left alone.
        var initial = char.IsUpper(last[0]) ? last[..1] : last[..1].ToUpper(TurkishCulture);
        return $"{first} {initial}.";
    }
}
