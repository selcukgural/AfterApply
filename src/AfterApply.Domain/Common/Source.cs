namespace AfterApply.Domain.Common;

public enum Source
{
    Manual,
    LinkedIn,
    KariyerNet,
    LinkedInImport,
    CsvImport,
    CompanyWebsite,
    Referral,
    BrowserExtension,
    Email,
    System,
    Other,

    // Applicant tracking systems — the page where the application is actually submitted, as
    // opposed to the aggregator that advertised it. Only ever used as Job.Source (provenance of
    // the posting), never as Application.Source (which stays BrowserExtension/Manual/...), and
    // only reachable through JobPostingSourceResolver. Persisted as strings
    // (HasConversion<string>), so adding a member costs no column migration.
    Greenhouse,
    Lever,
    Ashby,
    Workday,
    Workable,
    SmartRecruiters
}
