using AfterApply.Application.Blog;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Ganss.Xss;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Blog;

/// <summary>
/// The allowlist between the editor and the public page. Everything the editor can emit and the
/// page is meant to show is named here; everything else — scripts, event handlers, iframes,
/// forms, unknown attributes, CSS beyond the five presentational properties, any URL scheme but
/// http(s) — is dropped by construction rather than by pattern-matching what an attack looks like.
///
/// Two rules of its own on top of Ganss's allowlists:
/// <list type="bullet">
/// <item>An <c>&lt;img&gt;</c> may only point at one of our own media objects
/// (<see cref="BlogMediaPath"/>). An absolute URL to that path is folded to the relative form; any
/// other source — a hotlinked image, a data: URI — removes the element. The post's images are the
/// ones that were uploaded through the post, full stop.</item>
/// <item>A link to another site opens in a new tab with <c>rel="noopener noreferrer nofollow"</c>:
/// noopener so the target cannot script the opener, nofollow because the blog must not become a
/// place to buy links from. A link to our own site written out in full
/// (<c>https://ekariyerim.com/tr/guide/…</c>, the host of <see cref="AppOptions.WebBaseUrl"/>, with
/// or without <c>www.</c>) is folded to its path first, so it stays an internal link: pasting
/// from anywhere but the site itself — a copied page, a document — hands the editor absolute
/// URLs, and those must not come out as nofollow links to ourselves.</item>
/// </list>
/// Thread-safe once built, so registered as a singleton.
/// </summary>
public sealed class BlogHtmlSanitizer : IBlogHtmlSanitizer
{
    private static readonly string[] AllowedTags =
    [
        "p", "h1", "h2", "h3", "h4", "strong", "b", "em", "i", "u", "s", "sub", "sup", "mark", "a", "img",
        "ul", "ol", "li", "blockquote", "pre", "code", "hr", "br", "span", "figure", "figcaption",
        "table", "thead", "tbody", "tr", "th", "td", "label", "input", "div"
    ];

    private static readonly string[] AllowedAttributes =
    [
        "href", "target", "rel", "src", "alt", "title", "width", "height", "style", "colspan", "rowspan",
        // Task lists: Tiptap renders a checkbox per item and marks the list with data-type.
        "type", "checked", "disabled", "data-type", "data-checked", "start", "class"
    ];

    private static readonly string[] AllowedCssProperties =
    [
        "color", "background-color", "font-family", "font-size", "text-align", "line-height", "width"
    ];

    private readonly HtmlSanitizer _sanitizer;
    private readonly string? _siteHost;

    public BlogHtmlSanitizer(IOptions<AppOptions> appOptions)
    {
        _siteHost = Uri.TryCreate(appOptions.Value.WebBaseUrl, UriKind.Absolute, out var webBase)
            ? StripWww(webBase.Host)
            : null;

        _sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(AllowedTags, StringComparer.OrdinalIgnoreCase),
            AllowedAttributes = new HashSet<string>(AllowedAttributes, StringComparer.OrdinalIgnoreCase),
            AllowedCssProperties = new HashSet<string>(AllowedCssProperties, StringComparer.OrdinalIgnoreCase),
            AllowedSchemes = new HashSet<string>(["http", "https"], StringComparer.OrdinalIgnoreCase),
            UriAttributes = new HashSet<string>(["href", "src"], StringComparer.OrdinalIgnoreCase),
            AllowedAtRules = new HashSet<AngleSharp.Css.Dom.CssRuleType>()
        })
        {
            // An <img> with no usable src is not an image; a <a> with no href is fine (an anchor).
            KeepChildNodes = false,
            AllowDataAttributes = false
        };

        // "class" is allowed only so Tiptap's own structural classes survive; nothing in the
        // public stylesheet keys off arbitrary class names, so a stray one is inert.
        _sanitizer.PostProcessNode += OnPostProcessNode;
    }

    public string Sanitize(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return _sanitizer.Sanitize(html);
    }

    private void OnPostProcessNode(object? sender, PostProcessNodeEventArgs e)
    {
        switch (e.Node)
        {
            case IHtmlImageElement image:
                FoldImageSource(image);
                break;
            case IHtmlAnchorElement anchor:
                FoldOwnSiteLink(anchor);
                HardenAnchor(anchor);
                break;
            case IHtmlInputElement input:
                // The only <input> the editor emits is a task-list checkbox, and on the public
                // page it is inert. Anything else is not a thing a post contains.
                if (!string.Equals(input.Type, "checkbox", StringComparison.OrdinalIgnoreCase))
                {
                    input.Remove();
                }
                else
                {
                    input.SetAttribute("disabled", "disabled");
                }

                break;
        }
    }

    private static void FoldImageSource(IHtmlImageElement image)
    {
        var mediaId = BlogMediaPath.Parse(image.GetAttribute("src"));
        if (mediaId is null)
        {
            image.Remove();
            return;
        }

        image.SetAttribute("src", BlogMediaPath.For(mediaId.Value));
        image.SetAttribute("loading", "lazy");
    }

    /// <summary><c>https://ekariyerim.com/tr/guide/x?y#z</c> → <c>/tr/guide/x?y#z</c>; any other
    /// href is left alone. Scheme is not checked beyond http(s) — the allowlist already ran.</summary>
    private void FoldOwnSiteLink(IHtmlAnchorElement anchor)
    {
        if (_siteHost is null
            || !Uri.TryCreate(anchor.GetAttribute("href"), UriKind.Absolute, out var href)
            || (href.Scheme != Uri.UriSchemeHttps && href.Scheme != Uri.UriSchemeHttp)
            || !string.Equals(StripWww(href.Host), _siteHost, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        anchor.SetAttribute("href", href.PathAndQuery + href.Fragment);
    }

    private static string StripWww(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;

    private static void HardenAnchor(IHtmlAnchorElement anchor)
    {
        var href = anchor.GetAttribute("href");
        if (string.IsNullOrEmpty(href))
        {
            return;
        }

        // Relative links and anchors within the page are ours; only a link that names a host is
        // "another site". The scheme allowlist has already refused javascript: and data:, so an
        // absolute link here is http(s). (Not Uri.TryCreate(Absolute): on Unix a rooted path
        // parses as a file: URI and every internal link would be "external".)
        if (!href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            anchor.RemoveAttribute("target");
            anchor.RemoveAttribute("rel");
            return;
        }

        anchor.SetAttribute("target", "_blank");
        anchor.SetAttribute("rel", "noopener noreferrer nofollow");
    }
}
