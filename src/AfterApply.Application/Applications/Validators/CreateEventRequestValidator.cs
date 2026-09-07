using System.Text.Json;
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
        // Metadata is a jsonb column. Without this rule anything that is not valid JSON reaches
        // Postgres and comes back as an unhandled 22P02, i.e. a 500 for what is squarely a bad
        // request — which is exactly what the new "add an event" form hit on its first run.
        RuleFor(x => x.Metadata).MaximumLength(4000);
        RuleFor(x => x.Metadata)
            .Must(BeValidJson)
            .When(x => !string.IsNullOrWhiteSpace(x.Metadata))
            .WithMessage(_ => localizer["VALIDATION_EVENT_METADATA_MUST_BE_JSON"]);
    }

    private static bool BeValidJson(string? metadata)
    {
        try
        {
            using var _ = JsonDocument.Parse(metadata!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
