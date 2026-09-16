namespace AfterApply.Domain.Mailing;

public enum EmailTemplateKey
{
    PasswordReset,
    PasswordChanged,
    /// <summary>The Monday digest of the paid weekly job matching: how many postings are ready and
    /// the best of them. Placeholders: {{Count}}, {{BestTitle}}, {{BestCompany}}, {{BestScore}}, {{Link}}.</summary>
    WeeklyJobsReady,
    /// <summary>PayTR confirmed a Pro payment. Placeholders: {{PlanName}}, {{Amount}}, {{ActiveUntil}}, {{OrdersLink}}.</summary>
    PaymentReceived,
    /// <summary>The prepaid Pro period ends in a few days. Placeholders: {{ActiveUntil}}, {{RenewLink}}.</summary>
    ProExpiring,
    /// <summary>A refund was sent back to the card. Placeholders: {{Amount}}.</summary>
    RefundCompleted,
    /// <summary>A refund request was turned down, with the admin's note. Placeholders: {{Note}}.</summary>
    RefundRejected
}
