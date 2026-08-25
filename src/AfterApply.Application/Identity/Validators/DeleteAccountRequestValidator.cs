using AfterApply.Application.Identity.Contracts;
using FluentValidation;

namespace AfterApply.Application.Identity.Validators;

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
    }
}
