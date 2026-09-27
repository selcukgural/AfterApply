using AfterApply.Application.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Applications.Validators;

/// <summary>The range an interview date must fall in, shared by the status change and the stand-alone
/// setter. A month back covers "I forgot to write it down last week"; a year ahead covers any real
/// process. Anything outside is a typo that would put a question on the dashboard at a random time.</summary>
internal static class InterviewRules
{
    public const int MaxDaysBack = 30;
    public const int MaxDaysAhead = 365;

    public static bool IsInRange(DateTimeOffset? interviewAt)
    {
        if (interviewAt is not { } at)
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        return at >= now.AddDays(-MaxDaysBack) && at <= now.AddDays(MaxDaysAhead);
    }

    public static IRuleBuilderOptions<T, DateTimeOffset?> MustBeAReasonableInterviewDate<T>(
        this IRuleBuilder<T, DateTimeOffset?> rule, IStringLocalizer<SharedStrings> localizer)
    {
        return rule.Must(IsInRange).WithMessage(_ => localizer["VALIDATION_INTERVIEW_RANGE"]);
    }
}
