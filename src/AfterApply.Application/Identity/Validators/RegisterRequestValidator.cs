using AfterApply.Application.Identity.Contracts;
using FluentValidation;

namespace AfterApply.Application.Identity.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ConsentAccepted).Must(x => x).WithMessage("Gizlilik politikasını kabul etmelisiniz.");
    }
}
