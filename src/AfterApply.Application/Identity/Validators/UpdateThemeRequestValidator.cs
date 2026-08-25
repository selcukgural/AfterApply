using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Identity.Validators;

public sealed class UpdateThemeRequestValidator : AbstractValidator<UpdateThemeRequest>
{
    private static readonly string[] SupportedThemes = ["light", "dark"];

    public UpdateThemeRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Theme).NotEmpty()
            .Must(theme => SupportedThemes.Contains(theme))
            .WithMessage(_ => localizer["VALIDATION_UNSUPPORTED_THEME"]);
    }
}
