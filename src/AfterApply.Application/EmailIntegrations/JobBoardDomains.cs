namespace AfterApply.Application.EmailIntegrations;

// Deliberately small, hand-curated set of well-known job board/ATS vendor domains — same
// philosophy as RuleBasedEmailClassifier next to it: broader coverage comes from the per-user
// application-domain match (see EmailForwardingService.BuildCandidatesAsync), not from growing
// this table. This list only decides whether an unmatched sender is trusted enough to justify an
// LLM classification call, never whether an email gets processed at all.
public static class JobBoardDomains
{
    private static readonly string[] Domains =
    [
        // Global job boards
        "linkedin.com", "indeed.com", "glassdoor.com", "ziprecruiter.com", "monster.com",
        "careerbuilder.com", "simplyhired.com", "dice.com", "wellfound.com", "hired.com", "xing.com",

        // ATS / recruiting platform vendors (companies' career pages/mail run on these)
        "greenhouse.io", "lever.co", "workable.com", "smartrecruiters.com", "icims.com",
        "taleo.net", "successfactors.com", "sapsf.com", "myworkdayjobs.com", "workday.com",
        "jobvite.com", "breezy.hr", "bamboohr.com", "ashbyhq.com", "personio.com", "personio.de",
        "recruitee.com", "teamtailor.com", "join.com", "freshteam.com", "jazzhr.com",
        "applytojob.com", "ultipro.com", "myworkforcenow.com", "paylocity.com", "paycomonline.com",
        "adp.com", "cornerstoneondemand.com", "phenompeople.com", "eightfold.ai",

        // Turkish job boards
        "kariyer.net", "secretcv.com", "isbul.net", "iskur.gov.tr", "yenibiris.com",
    ];

    public static bool IsKnown(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        foreach (var known in Domains)
        {
            if (domain.Equals(known, StringComparison.OrdinalIgnoreCase) ||
                domain.EndsWith("." + known, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
