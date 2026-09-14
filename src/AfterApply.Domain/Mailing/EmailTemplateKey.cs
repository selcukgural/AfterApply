namespace AfterApply.Domain.Mailing;

public enum EmailTemplateKey
{
    PasswordReset,
    PasswordChanged,
    /// <summary>The Monday digest of the paid weekly job matching: how many postings are ready and
    /// the best of them. Placeholders: {{Count}}, {{BestTitle}}, {{BestCompany}}, {{BestScore}}, {{Link}}.</summary>
    WeeklyJobsReady
}
