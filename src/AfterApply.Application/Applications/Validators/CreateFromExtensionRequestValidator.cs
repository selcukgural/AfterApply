using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Common;
using FluentValidation;

namespace AfterApply.Application.Applications.Validators;

public sealed class CreateFromExtensionRequestValidator : AbstractValidator<CreateFromExtensionRequest>
{
    public CreateFromExtensionRequestValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(300);
        RuleFor(x => x.JobUrl).NotEmpty().MaximumLength(2000).MustBeAWebUrl();
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(10_000);
        RuleFor(x => x.DescriptionHtml).MaximumLength(20_000);

        // Stored and later fetched server-side by CompanyEnrichmentService — restricted to
        // linkedin.com so a malicious/compromised client can't turn this into an SSRF vector
        // (e.g. pointing the background fetch at an internal address).
        RuleFor(x => x.CompanyLinkedInUrl)
            .MaximumLength(500)
            .Must(url => BeAnAllowedProfileUrl(url, "linkedin.com"))
            .WithMessage("CompanyLinkedInUrl must be an https://www.linkedin.com/company/... URL.")
            .When(x => x.CompanyLinkedInUrl is not null);

        // Same reasoning as CompanyLinkedInUrl above: stored, then fetched server-side by
        // CompanyEnrichmentService, so it is pinned to kariyer.net rather than left as any URL a
        // client felt like sending.
        RuleFor(x => x.CompanyKariyerNetUrl)
            .MaximumLength(500)
            .Must(url => BeAnAllowedProfileUrl(url, "kariyer.net"))
            .WithMessage("CompanyKariyerNetUrl must be an https://www.kariyer.net/firma-profil/... URL.")
            .When(x => x.CompanyKariyerNetUrl is not null);
    }

    private static bool BeAnAllowedProfileUrl(string? url, string domain) =>
        url is not null
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
}
