using AfterApply.Application.Identity.Contracts;
using FluentValidation;

namespace AfterApply.Application.Identity.Validators;

public sealed class StartExtensionPairingRequestValidator : AbstractValidator<StartExtensionPairingRequest>
{
    public StartExtensionPairingRequestValidator()
    {
        // Absent is fine — the service falls back to the default locale. Present and unknown is
        // not: it would otherwise be pasted into the verification URL, and a URL built from
        // caller-supplied text is how an open redirect starts.
        RuleFor(r => r.Locale)
            .Must(locale => locale is "tr" or "en")
            .When(r => r.Locale is not null);
    }
}

public sealed class PollExtensionPairingRequestValidator : AbstractValidator<PollExtensionPairingRequest>
{
    /// <summary>The generated secret is 32 random bytes as Base64Url — 43 characters. The bound is
    /// there to keep an oversized body from reaching the hash function, not to check the shape.
    /// </summary>
    private const int MaxDeviceSecretLength = 128;

    public PollExtensionPairingRequestValidator()
    {
        RuleFor(r => r.DeviceSecret).NotEmpty().MaximumLength(MaxDeviceSecretLength);
    }
}
