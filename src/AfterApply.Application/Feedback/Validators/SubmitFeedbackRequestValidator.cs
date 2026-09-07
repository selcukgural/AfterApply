using AfterApply.Application.Feedback.Contracts;
using FluentValidation;

namespace AfterApply.Application.Feedback.Validators;

public sealed class SubmitFeedbackRequestValidator : AbstractValidator<SubmitFeedbackRequest>
{
    /// <summary>Matches the column width and the panel's own counter. Long enough for a real bug
    /// report, short enough that the endpoint is not a place to park data.</summary>
    public const int MaxMessageLength = 1000;

    public SubmitFeedbackRequestValidator()
    {
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Mood).IsInEnum().When(x => x.Mood.HasValue);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(MaxMessageLength);
        RuleFor(x => x.ReplyEmail).EmailAddress().MaximumLength(320)
            .When(x => !string.IsNullOrWhiteSpace(x.ReplyEmail));

        // Context, not content: the client fills these in, so they are bounded rather than trusted.
        // A path, never a full URL — a query string can carry ids we have no reason to store.
        RuleFor(x => x.PagePath).MaximumLength(200)
            .Must(path => path is null || path.StartsWith('/'))
            .WithMessage("'Page Path' must be a path beginning with '/'.");
        RuleFor(x => x.Locale).MaximumLength(10);
        RuleFor(x => x.Theme).MaximumLength(10);
    }
}
