using System.Text.RegularExpressions;

namespace AfterApply.Application.Blog;

/// <summary>
/// The one address a blog image has: <c>/api/blog/media/{id}</c>, relative. Relative on purpose —
/// the HTML is stored once and rendered by the web app, which serves that path from its own origin
/// (a rewrite to the API), so the stored markup does not carry a hostname that differs between a
/// laptop, a preview and production. The sanitizer accepts an absolute form and folds it to this.
/// </summary>
public static partial class BlogMediaPath
{
    public const string Prefix = "/api/blog/media/";

    public static string For(Guid mediaId) => $"{Prefix}{mediaId:D}";

    /// <summary>The media id in a URL that is (or ends with) a blog media path, else null.
    /// Accepts the relative path and any http(s) URL whose path is one.</summary>
    public static Guid? Parse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var match = MediaPathRegex().Match(url.Trim());
        return match.Success && Guid.TryParseExact(match.Groups["id"].Value, "D", out var id) ? id : null;
    }

    [GeneratedRegex(@"^(?:https?://[^/?#]+)?/api/blog/media/(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/?$")]
    private static partial Regex MediaPathRegex();
}
