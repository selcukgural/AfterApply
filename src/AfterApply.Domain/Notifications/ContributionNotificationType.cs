namespace AfterApply.Domain.Notifications;

/// <summary>Which kind of contribution was found helpful. Stored as a string; the name is also the
/// key the web uses to pick the sentence and the link.</summary>
public enum ContributionNotificationType
{
    ReviewHelpful,
    SalaryHelpful,
    ExperienceHelpful,
    BlogCommentHelpful
}
