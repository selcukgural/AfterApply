using AfterApply.Application.Applications.Contracts;
using AfterApply.Domain.Applications;
using FluentValidation;

namespace AfterApply.Application.Applications.Validators;

public sealed class CreateEventRequestValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum()
            .NotEqual(ApplicationEventType.StatusChanged)
            .WithMessage("StatusChanged events can only be created via the status-change endpoint.");
        RuleFor(x => x.Source).IsInEnum().When(x => x.Source.HasValue);
        RuleFor(x => x.Metadata).MaximumLength(4000);
    }
}
