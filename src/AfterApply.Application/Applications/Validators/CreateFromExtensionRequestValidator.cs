using AfterApply.Application.Applications.Contracts;
using FluentValidation;

namespace AfterApply.Application.Applications.Validators;

public sealed class CreateFromExtensionRequestValidator : AbstractValidator<CreateFromExtensionRequest>
{
    public CreateFromExtensionRequestValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(300);
        RuleFor(x => x.JobUrl).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(10_000);
        RuleFor(x => x.DescriptionHtml).MaximumLength(20_000);
    }
}
