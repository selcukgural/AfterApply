using AfterApply.Application.Notifications.Contracts;
using FluentValidation;

namespace AfterApply.Application.Notifications.Validators;

public sealed class GetRemindersQueryValidator : AbstractValidator<GetRemindersQuery>
{
    public GetRemindersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class BulkReminderRequestValidator : AbstractValidator<BulkReminderRequest>
{
    /// <summary>
    /// An id list is what the user could tick on one page, so it is small by construction; the
    /// ceiling only exists so the alternative — pasting the whole list in — is refused at the door
    /// rather than handled. Anything wider is what <see cref="ReminderSelection.All"/> is for.
    /// </summary>
    public const int MaxIds = 100;

    public BulkReminderRequestValidator()
    {
        RuleFor(x => x.Selection).NotNull()
            .Must(selection => selection.Ids is not null ^ selection.All)
            .WithMessage("Provide either an explicit id list or all, not both.")
            .Must(selection => selection.Ids is null || selection.Ids.Count > 0)
            .WithMessage("The id list cannot be empty.")
            .Must(selection => selection.Ids is null || selection.Ids.Count <= MaxIds)
            .WithMessage($"The id list can hold at most {MaxIds} reminders; use all for a wider selection.")
            .Must(selection => selection.Ids is null || selection.Ids.Distinct().Count() == selection.Ids.Count)
            .WithMessage("The id list cannot repeat a reminder.");
        RuleFor(x => x.ExpectedCount)
            .Must((request, expectedCount) => !request.Selection.All || expectedCount is >= 0)
            .When(x => x.Selection is not null)
            .WithMessage("An all selection must state the count it was shown.");
    }
}
