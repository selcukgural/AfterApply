using AfterApply.Application.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Applications.Validators;

/// <summary>The one range a promised reply date must fall in, shared by the status change and the
/// stand-alone setter. A year either side of today: a past date is legitimate ("they said last
/// Friday") and so is a slow process, but a date years away is a typo that would silence the
/// follow-up reminder for good.</summary>
internal static class ReplyPromiseRules
{
    public const int MaxDaysFromToday = 365;

    public static bool IsInRange(DateOnly? promisedBy)
    {
        if (promisedBy is not { } date)
        {
            return true;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return date >= today.AddDays(-MaxDaysFromToday) && date <= today.AddDays(MaxDaysFromToday);
    }

    public static IRuleBuilderOptions<T, DateOnly?> MustBeAReasonableReplyDate<T>(
        this IRuleBuilder<T, DateOnly?> rule, IStringLocalizer<SharedStrings> localizer)
    {
        return rule.Must(IsInRange).WithMessage(_ => localizer["VALIDATION_REPLY_PROMISE_RANGE"]);
    }
}
