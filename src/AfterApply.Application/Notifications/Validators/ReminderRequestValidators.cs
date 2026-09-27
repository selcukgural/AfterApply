using AfterApply.Application.Applications.Validators;
using AfterApply.Application.Localization;
using AfterApply.Application.Notifications.Contracts;
using AfterApply.Domain.Applications;
using FluentValidation;
using Microsoft.Extensions.Localization;

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

public sealed class PauseRemindersRequestValidator : AbstractValidator<PauseRemindersRequest>
{
    /// <summary>The three lengths the profile card offers: a week, a fortnight, a month.</summary>
    public static readonly IReadOnlyCollection<int> AllowedDays = [7, 14, 30];

    public PauseRemindersRequestValidator()
    {
        RuleFor(x => x.Days)
            .Must(days => AllowedDays.Contains(days))
            .WithMessage($"Days must be one of {string.Join(", ", AllowedDays)}.");
    }
}

public sealed class SnoozeReminderRequestValidator : AbstractValidator<SnoozeReminderRequest>
{
    /// <summary>What the row's menu offers: "ask tomorrow" and "in 3 days" on an interview question,
    /// 3 days / a week / a fortnight on the rest.</summary>
    public static readonly IReadOnlyCollection<int> AllowedDays = [1, 3, 7, 14];

    public SnoozeReminderRequestValidator()
    {
        RuleFor(x => x.Days)
            .Must(days => AllowedDays.Contains(days))
            .WithMessage($"Days must be one of {string.Join(", ", AllowedDays)}.");
    }
}

public sealed class InterviewOutcomeRequestValidator : AbstractValidator<InterviewOutcomeRequest>
{
    /// <summary>Where a process can go after an interview. Screening is not among them: it comes
    /// before interviews, and "moved on to screening" would be a step back recorded as progress.</summary>
    public static readonly HashSet<ApplicationStatus> NextStatuses =
    [
        ApplicationStatus.Interview, ApplicationStatus.TechnicalInterview, ApplicationStatus.FinalInterview,
        ApplicationStatus.Offer, ApplicationStatus.Accepted
    ];

    public InterviewOutcomeRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Outcome).IsInEnum();
        RuleFor(x => x.NextStatus)
            .Must(status => status is { } s && NextStatuses.Contains(s))
            .When(x => x.Outcome == InterviewOutcome.NextStage)
            .WithMessage(_ => localizer["VALIDATION_INTERVIEW_NEXT_STATUS"]);
        RuleFor(x => x.NextStatus).Null().When(x => x.Outcome != InterviewOutcome.NextStage);
        RuleFor(x => x.PromisedReplyBy).MustBeAReasonableReplyDate(localizer);
        RuleFor(x => x.PromisedReplyBy).Null().When(x => x.Outcome != InterviewOutcome.Waiting);
    }
}

public sealed class UndoInterviewOutcomeRequestValidator : AbstractValidator<UndoInterviewOutcomeRequest>
{
    public UndoInterviewOutcomeRequestValidator()
    {
        RuleFor(x => x.FromStatus).IsInEnum();
        RuleFor(x => x.ToStatus).IsInEnum();
        // A status move is undone from both of its ends or not at all.
        RuleFor(x => x.ToStatus).NotNull().When(x => x.FromStatus is not null);
        RuleFor(x => x.FromStatus).NotNull().When(x => x.ToStatus is not null);
    }
}
