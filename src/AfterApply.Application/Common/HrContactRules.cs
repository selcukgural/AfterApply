using FluentValidation;

namespace AfterApply.Application.Common;

/// <summary>
/// The HR contact fields are accepted at three call sites (create/update an application, create a
/// tracked job) and must agree on what they allow, so the rules live here once rather than being
/// retyped three times.
///
/// All three are optional everywhere: an application usually has no contact at all, and clearing a
/// field the user emptied has to stay legal.
/// </summary>
public static class HrContactRules
{
    public static void ApplyHrContactRules<T>(this AbstractValidator<T> validator,
        Func<T, string?> name, Func<T, string?> email, Func<T, string?> linkedInUrl)
    {
        validator.RuleFor(x => name(x)).MaximumLength(200).WithName("HrName");

        validator.RuleFor(x => email(x))
            .MaximumLength(320)
            .EmailAddress()
            .WithName("HrEmail")
            .When(x => !string.IsNullOrWhiteSpace(email(x)));

        validator.RuleFor(x => linkedInUrl(x))
            .MaximumLength(500)
            .Must(BeALinkedInProfileUrl)
            .WithMessage("'HrLinkedInUrl' must be an https://www.linkedin.com/in/... profile URL.")
            .WithName("HrLinkedInUrl")
            .When(x => !string.IsNullOrWhiteSpace(linkedInUrl(x)));
    }

    /// <summary>
    /// Unlike the company-profile URLs, this one is never fetched server-side, so pinning it to
    /// linkedin.com is not an SSRF boundary — it is what the field means. The UI labels it as a
    /// LinkedIn profile and renders it behind a LinkedIn icon, so accepting an arbitrary URL would
    /// just produce a link that lies about where it goes. The https-only part does matter on its
    /// own: this value ends up in an href (see the web app's safeExternalUrl, which is the
    /// render-side half of the same guard).
    /// </summary>
    private static bool BeALinkedInProfileUrl(string? url) =>
        url is not null
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".linkedin.com", StringComparison.OrdinalIgnoreCase))
        && uri.AbsolutePath.StartsWith("/in/", StringComparison.OrdinalIgnoreCase);
}
