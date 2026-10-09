using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Notifications;
using FluentValidation;

namespace AfterApply.Application.Applications.Validators;

public sealed class SetReapplyReminderRequestValidator : AbstractValidator<SetReapplyReminderRequest>
{
    public SetReapplyReminderRequestValidator()
    {
        // Allow-listed, not ranged: the page offers three lengths and nothing else should arrive.
        RuleFor(x => x.Months).Must(months => ReapplyReminders.AllowedMonths.Contains(months));
    }
}
