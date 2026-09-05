using AfterApply.Application.Identity.Contracts;
using FluentValidation;

namespace AfterApply.Application.Identity.Validators;

public sealed class GoogleSignInRequestValidator : AbstractValidator<GoogleSignInRequest>
{
    // RFC 7636 §4.1: the verifier is 43–128 characters from the unreserved set. Anything else
    // never came from our own callback page, so reject it before spending a round-trip to Google.
    private const int MinCodeVerifierLength = 43;
    private const int MaxCodeVerifierLength = 128;

    public GoogleSignInRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.CodeVerifier).NotEmpty().Length(MinCodeVerifierLength, MaxCodeVerifierLength)
            .Matches("^[A-Za-z0-9._~-]+$");
        RuleFor(x => x.RedirectUri).NotEmpty().MaximumLength(2048)
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
                         && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps));
    }
}
