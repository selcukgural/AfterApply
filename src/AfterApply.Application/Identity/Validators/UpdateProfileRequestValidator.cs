using AfterApply.Application.Identity.Contracts;
using FluentValidation;

namespace AfterApply.Application.Identity.Validators;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        // The same rule as registration (RegisterRequestValidator): a name is optional there since
        // 2026-09-14, so the profile page must let someone keep — or clear — an empty one. The
        // columns are NOT NULL; an empty string goes in and the display name falls back to the
        // e-mail's local part (web: displayName.ts).
        RuleFor(x => x.FirstName).NotNull().MaximumLength(100);
        RuleFor(x => x.LastName).NotNull().MaximumLength(100);
    }
}
