using AfterApply.Domain.Notifications;

namespace AfterApply.Infrastructure.Notifications;

/// <summary>Whether an account's settings let a contribution notification of this type be written.
/// The master switch wins; a kind that is off stays off whatever the master says.</summary>
public static class NotificationPreferenceRules
{
    public static bool Allows(ContributionNotificationType type, bool contributions, bool reviewHelpful, bool salaryHelpful,
        bool experienceHelpful, bool blogCommentHelpful) =>
        contributions && type switch
        {
            ContributionNotificationType.ReviewHelpful => reviewHelpful,
            ContributionNotificationType.SalaryHelpful => salaryHelpful,
            ContributionNotificationType.ExperienceHelpful => experienceHelpful,
            ContributionNotificationType.BlogCommentHelpful => blogCommentHelpful,
            _ => false
        };
}
