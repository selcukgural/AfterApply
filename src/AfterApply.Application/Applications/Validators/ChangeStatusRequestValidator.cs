using AfterApply.Application.Applications.Contracts;
using FluentValidation;

namespace AfterApply.Application.Applications.Validators;

public sealed class ChangeStatusRequestValidator : AbstractValidator<ChangeStatusRequest>
{
    public ChangeStatusRequestValidator()
    {
        RuleFor(x => x.NewStatus).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
