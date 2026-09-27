using AfterApply.Application.FeatureFlags.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.FeatureFlags;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.FeatureFlags.Validators;

public static class FeatureFlagLimits
{
    public const int DefaultHistory = 50;

    public const int MaxHistory = 200;

    /// <summary>A data-protection token is a few hundred characters; anything far past that is not one.</summary>
    public const int MaxTokenLength = 4096;

    public const int MaxPhraseLength = 64;
}

public sealed class PrepareFeatureFlagChangeRequestValidator : AbstractValidator<PrepareFeatureFlagChangeRequest>
{
    public PrepareFeatureFlagChangeRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Reason)
            .Must(reason => reason is not null && reason.Trim().Length >= FeatureFlagChange.MinReasonLength)
            .WithErrorCode("FEATURE_FLAG_REASON_REQUIRED")
            .WithMessage(_ => localizer["FEATURE_FLAG_REASON_REQUIRED"]);
        RuleFor(x => x.Reason)
            .MaximumLength(FeatureFlagChange.MaxReasonLength)
            .WithErrorCode("FEATURE_FLAG_REASON_TOO_LONG")
            .WithMessage(_ => localizer["FEATURE_FLAG_REASON_TOO_LONG"]);
    }
}

public sealed class ConfirmFeatureFlagChangeRequestValidator : AbstractValidator<ConfirmFeatureFlagChangeRequest>
{
    public ConfirmFeatureFlagChangeRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.ConfirmationToken)
            .NotEmpty()
            .MaximumLength(FeatureFlagLimits.MaxTokenLength)
            .WithErrorCode("FEATURE_FLAG_CONFIRMATION_INVALID")
            .WithMessage(_ => localizer["FEATURE_FLAG_CONFIRMATION_INVALID"]);
        RuleFor(x => x.ConfirmationPhrase)
            .NotEmpty()
            .MaximumLength(FeatureFlagLimits.MaxPhraseLength)
            .WithErrorCode("FEATURE_FLAG_PHRASE_MISMATCH")
            .WithMessage(_ => localizer["FEATURE_FLAG_PHRASE_MISMATCH"]);
    }
}
