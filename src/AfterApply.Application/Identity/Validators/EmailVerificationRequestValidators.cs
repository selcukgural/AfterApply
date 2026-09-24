using AfterApply.Application.Identity.Contracts;
using FluentValidation;

namespace AfterApply.Application.Identity.Validators;

public sealed class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.VerificationTicket).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{6}$");
    }
}

public sealed class ResendVerificationCodeRequestValidator : AbstractValidator<ResendVerificationCodeRequest>
{
    public ResendVerificationCodeRequestValidator()
    {
        RuleFor(x => x.VerificationTicket).NotEmpty().MaximumLength(128);
    }
}
