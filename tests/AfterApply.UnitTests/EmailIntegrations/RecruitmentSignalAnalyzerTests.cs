using AfterApply.Application.EmailIntegrations;
using Shouldly;

namespace AfterApply.UnitTests.EmailIntegrations;

public class RecruitmentSignalAnalyzerTests
{
    // EmailIntelligenceOptions/Weights/Phrases carry no C# defaults (see their own doc comments —
    // production values live in appsettings.json, validated at startup). Tests build a complete,
    // self-contained instance here rather than depending on appsettings.json, same as
    // RuleBasedEmailClassifierTests/EmailApplicationMatcherTests use inline literal data.
    private static readonly EmailIntelligenceWeights Weights = new()
    {
        MatchedApplication = 35,
        ApplicationPhrase = 30,
        InterviewPhrase = 35,
        AssessmentPhrase = 30,
        OfferPhrase = 35,
        RecruiterSignal = 10,
        KnownJobBoardOrAts = 20,
        CalendarLink = 15,
        ApplicationLink = 10,
        CompanyNameInSubject = 20,
        Newsletter = -25,
        Unsubscribe = -25,
        Marketing = -25,
        JobAlert = -30,
        Digest = -20,
        ApplicationCap = 35,
        InterviewCap = 40,
        AssessmentCap = 35,
        OfferCap = 35,
        RecruiterCap = 15,
        AtsCap = 20,
        CompanyMatchCap = 35,
        LinksCap = 20,
        NegativeCap = -35
    };

    private static readonly EmailIntelligencePhrases Phrases = new()
    {
        Application = ["application update", "application status", "your application", "applied for",
            "başvurunuz", "başvurunuzun durumu", "başvuru durumu"],
        Interview = ["interview invitation", "interview scheduled", "interview confirmation", "technical interview",
            "final interview", "screening interview", "phone screen", "video interview", "meet the team",
            "mülakat", "görüşme", "iş görüşmesi", "teknik görüşme", "görüşme daveti"],
        Assessment = ["assessment", "technical assessment", "coding assessment", "coding challenge",
            "online assessment", "skills assessment", "değerlendirme testi", "kodlama testi"],
        Offer = ["offer letter", "employment offer", "job offer", "compensation package", "congratulations",
            "iş teklifi", "teklif mektubu"],
        Recruiter = ["recruiter", "recruitment", "talent acquisition", "talent team", "hiring manager",
            "human resources", "candidate", "applicant", "işe alım", "aday"],
        RecruiterLocalPartPrefixes = ["recruiter", "recruitment", "talent", "careers", "jobs", "hiring", "hr-", "hr.", "hr_"],
        RecruiterLocalPartExact = ["hr"],
        Newsletter = ["newsletter", "bültenimiz", "e-bültenimiz"],
        Unsubscribe = ["unsubscribe", "abonelikten çık", "listeden çık"],
        Marketing = ["marketing", "promotional", "promotion", "kampanya", "tanıtım"],
        JobAlert = ["job alert", "recommended jobs", "jobs you may like", "jobs matching your profile",
            "size uygun ilanlar", "önerilen ilanlar"],
        Digest = ["digest", "weekly digest", "daily digest", "haftalık özet"],
        AtsLinkDomains = ["greenhouse.io", "lever.co", "myworkdayjobs.com", "workday.com", "smartrecruiters.com",
            "ashbyhq.com", "teamtailor.com", "personio.com", "personio.de"],
        CalendarLinkDomains = ["calendly.com", "zoom.us", "teams.microsoft.com", "meet.google.com"]
    };

    private static readonly EmailIntelligenceOptions Options = new()
    {
        LowThreshold = 20,
        LlmThreshold = 50,
        HighConfidenceThreshold = 70,
        Weights = Weights,
        Phrases = Phrases
    };

    [Fact]
    public void Analyze_Unknown_Sender_With_Paraphrased_Interview_Text_Reaches_Llm_Threshold()
    {
        // Not one of RuleBasedEmailClassifier's narrow curated phrases (that would already have
        // returned a definitive result upstream and never reached this analyzer at all).
        var result = RecruitmentSignalAnalyzer.Analyze(
            "hiring@smallcompany.example", "Next steps", "We'd love to invite you for a technical interview and a quick phone screen with our team next week.",
            "smallcompany.example", isKnownJobBoardOrAtsDomain: false, hasApplicationMatch: false, linkDomains: [], Options);

        result.Score.ShouldBeGreaterThanOrEqualTo(50);
        result.Signals.ShouldContain(s => s.Category == "Interview");
        result.Signals.ShouldContain(s => s.Category == "Recruiter"); // hiring@ local-part
    }

    [Fact]
    public void Analyze_Personal_Weekend_Plans_Email_Scores_Zero()
    {
        var result = RecruitmentSignalAnalyzer.Analyze(
            "someone@random-personal-test.com", "Weekend plans", "Are we still on for Saturday?",
            "random-personal-test.com", isKnownJobBoardOrAtsDomain: false, hasApplicationMatch: false, linkDomains: [], Options);

        result.Score.ShouldBe(0);
        result.Signals.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Weekly Newsletter", "Check out our latest blog posts. Unsubscribe here.")]
    [InlineData("10 jobs you may be interested in", "Here are jobs matching your profile this week.")]
    public void Analyze_Newsletter_And_JobAlert_Text_Stays_Below_Llm_Threshold(string subject, string snippet)
    {
        var result = RecruitmentSignalAnalyzer.Analyze(
            "updates@some-board.example", subject, snippet, "some-board.example",
            isKnownJobBoardOrAtsDomain: false, hasApplicationMatch: false, linkDomains: [], Options);

        result.Score.ShouldBeLessThan(50);
    }

    [Fact]
    public void Analyze_Matched_Application_With_Marketing_Phrase_Nets_Both_Signals()
    {
        // Source plan's own acceptance criterion: company-domain match alone doesn't guarantee
        // the email is application-related (e.g. a newsletter from a company you also applied to).
        var result = RecruitmentSignalAnalyzer.Analyze(
            "newsletter@acme.com", "Acme Monthly Newsletter", "Check out our latest promotional offers.",
            "acme.com", isKnownJobBoardOrAtsDomain: false, hasApplicationMatch: true, linkDomains: [], Options);

        result.Signals.ShouldContain(s => s.Category == "MatchedApplication" && s.Weight > 0);
        result.Signals.ShouldContain(s => s.Category == "Marketing" && s.Weight < 0);
    }

    [Fact]
    public void Analyze_Repeated_Phrases_In_Same_Category_Does_Not_Exceed_Category_Cap()
    {
        var result = RecruitmentSignalAnalyzer.Analyze(
            "hr@company.example", "Interview", "interview invitation interview scheduled technical interview final interview screening interview phone screen video interview meet the team",
            "company.example", isKnownJobBoardOrAtsDomain: false, hasApplicationMatch: false, linkDomains: [], Options);

        var interviewSignal = result.Signals.Single(s => s.Category == "Interview");
        interviewSignal.Weight.ShouldBe(Weights.InterviewCap);
    }

    [Fact]
    public void Analyze_Known_Ats_Link_Domain_Contributes_Links_Signal()
    {
        var result = RecruitmentSignalAnalyzer.Analyze(
            "noreply@ats-vendor.example", "Application received", "We got your application.",
            "ats-vendor.example", isKnownJobBoardOrAtsDomain: false, hasApplicationMatch: false,
            linkDomains: ["boards.greenhouse.io", "calendly.com"], Options);

        var linksSignal = result.Signals.Single(s => s.Category == "Links");
        linksSignal.Weight.ShouldBe(Math.Min(Weights.LinksCap, Weights.ApplicationLink + Weights.CalendarLink));
    }

    [Fact]
    public void Analyze_Neutral_Local_Part_Does_Not_Add_Recruiter_Signal_On_Its_Own()
    {
        var result = RecruitmentSignalAnalyzer.Analyze(
            "marketing@company.example", "Company Update", "Just a quick company update, nothing else.",
            "company.example", isKnownJobBoardOrAtsDomain: false, hasApplicationMatch: false, linkDomains: [], Options);

        result.Signals.ShouldNotContain(s => s.Category == "Recruiter");
    }

    [Fact]
    public void Analyze_Known_Job_Board_Domain_Contributes_Positive_Signal_Without_Other_Evidence()
    {
        var result = RecruitmentSignalAnalyzer.Analyze(
            "jobs-noreply@linkedin.com", "Your application status changed", "There's an update on your recent application.",
            "linkedin.com", isKnownJobBoardOrAtsDomain: true, hasApplicationMatch: false, linkDomains: [], Options);

        result.Signals.ShouldContain(s => s.Category == "KnownJobBoardOrAts");
    }

    [Fact]
    public void Analyze_Company_Name_In_Subject_Adds_Weak_Signal()
    {
        var result = RecruitmentSignalAnalyzer.Analyze(
            "hr@acmecorp.example", "Update from AcmeCorp on your application", "We wanted to update you.",
            "acmecorp.example", isKnownJobBoardOrAtsDomain: false, hasApplicationMatch: false, linkDomains: [], Options);

        result.Signals.ShouldContain(s => s.Category == "CompanyNameInSubject");
    }
}
