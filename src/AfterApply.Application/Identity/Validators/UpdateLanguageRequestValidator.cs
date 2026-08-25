using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Identity.Validators;

public sealed class UpdateLanguageRequestValidator : AbstractValidator<UpdateLanguageRequest>
{
    private static readonly string[] SupportedLanguages = ["tr", "en"];

    public UpdateLanguageRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Language).NotEmpty()
            .Must(language => SupportedLanguages.Contains(language))
            .WithMessage(_ => localizer["VALIDATION_UNSUPPORTED_LANGUAGE"]);
    }
}
