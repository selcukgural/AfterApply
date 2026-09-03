using FluentValidation;

namespace AfterApply.Application.Common;

public static class JobUrlRules
{
    /// <summary>
    /// A job URL is stored and later rendered as a plain anchor href in the web app
    /// (applications/[id]/page.tsx, TrackedJobList.tsx). React does not filter dangerous schemes out
    /// of href — it warns in development and renders the attribute anyway — so a saved
    /// <c>javascript:</c> URL becomes a one-click script execution on our own origin, with the
    /// access and refresh tokens sitting in localStorage. Only the owner can see their own rows, so
    /// this is self-XSS rather than a cross-user hole, but "the victim has to be talked into pasting
    /// something" is a phishing step, not a security boundary.
    ///
    /// Applied wherever a user-supplied job URL is accepted; the URL is optional at every one of
    /// those call sites, so an empty value passes and only a non-empty one has to be a real
    /// absolute http(s) URL.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> MustBeAWebUrl<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(BeAWebUrl).WithMessage("'{PropertyName}' must be an http or https URL.");

    private static bool BeAWebUrl(string? url) =>
        string.IsNullOrWhiteSpace(url)
        || (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
