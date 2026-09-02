using AfterApply.Application.Identity.Contracts;
using FluentValidation;

namespace AfterApply.Application.Identity.Validators;

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        // Actual password policy (length/complexity) is enforced by Identity's
        // UserManager.ResetPasswordAsync — see RegisterRequestValidator for the same split.
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}
