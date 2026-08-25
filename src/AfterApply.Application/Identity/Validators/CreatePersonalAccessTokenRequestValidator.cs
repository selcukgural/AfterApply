using AfterApply.Application.Identity.Contracts;
using FluentValidation;

namespace AfterApply.Application.Identity.Validators;

public sealed class CreatePersonalAccessTokenRequestValidator : AbstractValidator<CreatePersonalAccessTokenRequest>
{
    public CreatePersonalAccessTokenRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
