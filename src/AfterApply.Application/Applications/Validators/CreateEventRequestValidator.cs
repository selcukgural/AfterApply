using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.Applications;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Applications.Validators;

public sealed class CreateEventRequestValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Type).IsInEnum()
            .NotEqual(ApplicationEventType.StatusChanged)
            .WithMessage(_ => localizer["VALIDATION_STATUS_CHANGED_EVENT_NOT_ALLOWED"]);
        RuleFor(x => x.Source).IsInEnum().When(x => x.Source.HasValue);
        RuleFor(x => x.Metadata).MaximumLength(4000);
    }
}
