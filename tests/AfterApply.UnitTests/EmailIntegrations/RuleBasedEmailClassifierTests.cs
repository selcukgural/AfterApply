using AfterApply.Application.EmailIntegrations;
using AfterApply.Domain.Applications;
using Shouldly;

namespace AfterApply.UnitTests.EmailIntegrations;

public class RuleBasedEmailClassifierTests
{
    [Theory]
    [InlineData("Interview invitation", "We'd like to invite you to an interview next week.")]
    [InlineData("", "You have been invited to interview with our team.")]
    [InlineData("Mülakat", "Sizi mülakata davet etmek isteriz.")]
    public void Classify_Interview_Phrases_Suggest_Interview(string subject, string snippet)
    {
        var result = RuleBasedEmailClassifier.Classify(subject, snippet);

        result.SuggestedStatus.ShouldBe(ApplicationStatus.Interview);
        result.MatchedRule.ShouldBe("InterviewInvitation");
        result.ConfidenceScore.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("Application update", "Unfortunately, we have decided to move forward with other candidates.")]
    [InlineData("Başvuru Sonucu", "Maalesef başvurunuz olumsuz sonuçlanmıştır.")]
    // Real example (2026-09-02, Mercell/Teamtailor): "regret to inform" + "proceed with other
    // candidates" is a common rejection phrasing that doesn't contain either of the two phrases
    // above word-for-word — it was silently dropped (NoMatch) until these phrases were added.
    [InlineData("Your application to Mercell",
        "I regret to inform you that we have decided to proceed with other candidates for the time being.")]
    // Real example (2026-09-02, Hoppinger, from a Jobs-label mailbox audit): Dutch rejection —
    // "helaas" ("unfortunately") wasn't covered even though EN/DE/TR equivalents already were.
    [InlineData("Een update over je sollicitatie",
        "Bedankt voor je sollicitatie voor de functie van .Net Developer. Helaas moeten we je laten weten dat je niet door bent naar de volgende ronde.")]
    public void Classify_Rejection_Phrases_Suggest_Rejected(string subject, string snippet)
    {
        var result = RuleBasedEmailClassifier.Classify(subject, snippet);

        result.SuggestedStatus.ShouldBe(ApplicationStatus.Rejected);
        result.MatchedRule.ShouldBe("Rejection");
    }

    [Fact]
    public void Classify_StillWaiting_Phrase_Returns_Null_Status_With_NonZero_Confidence()
    {
        var result = RuleBasedEmailClassifier.Classify("Application received", "We will get back to you soon.");

        result.SuggestedStatus.ShouldBeNull();
        result.MatchedRule.ShouldBe("StillWaiting");
        result.ConfidenceScore.ShouldBeGreaterThan(0);
    }

    [Theory]
    // Real examples (2026-08-31): a plain "we got your application" acknowledgement from an ATS,
    // in each of the three languages the phrase table covers.
    [InlineData("We have received your application!",
        "Dear Selçuk Thank you so much for your application! At Abacus Medicine Group, our employees are our biggest asset.")]
    [InlineData("Ihre Bewerbung als Senior Engineer / Your application as Senior Engineer",
        "Guten Tag Selçuk Güral, wir freuen uns über Dein Interesse an einer Tätigkeit bei uns und bedanken uns für Deine Bewerbung.")]
    [InlineData("Başvurunuz Alındı", "Başvurunuz için teşekkür ederiz, en kısa sürede değerlendireceğiz.")]
    // Real examples (2026-09-02, Jobs-label mailbox audit): a Turkish ATS ("...pozisyonu için
    // başvurunuzu aldık" — active voice, distinct from the passive "başvurunuz alındı" already
    // covered above) and a Dutch ATS acknowledgement (Lely/Thinkwise both used this exact wording).
    [InlineData("Padran - Senior / Lead .NET Developer Pozisyonu İçin Başvurunuz",
        "Senior / Lead .NET Developer pozisyonu için başvurunuzu aldık. Şu anda bu pozisyon için başvuruları değerlendiriyoruz.")]
    [InlineData("Bevestiging van je sollicitatie bij Lely",
        "Beste Selçuk, We hebben je sollicitatie ontvangen voor de functie Software Architect. Bedankt voor jouw interesse in een mogelijke carrière bij Lely.")]
    public void Classify_ApplicationReceived_Phrases_Return_Null_Status_With_NonZero_Confidence(string subject, string snippet)
    {
        var result = RuleBasedEmailClassifier.Classify(subject, snippet);

        result.SuggestedStatus.ShouldBeNull();
        result.MatchedRule.ShouldBe("ApplicationReceived");
        result.ConfidenceScore.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Classify_ApplicationReceived_Phrase_Loses_To_Interview_When_Both_Present()
    {
        // An interview invitation that also happens to thank the candidate for applying —
        // the more specific/actionable signal must win, not the generic acknowledgement.
        var result = RuleBasedEmailClassifier.Classify("Ihre Bewerbung als Senior Engineer",
            "Vielen Dank für Ihre Bewerbung. We would like to invite you to an interview next week.");

        result.SuggestedStatus.ShouldBe(ApplicationStatus.Interview);
        result.MatchedRule.ShouldBe("InterviewInvitation");
    }

    [Fact]
    public void Classify_No_Match_Returns_Null_Status_And_Zero_Confidence()
    {
        var result = RuleBasedEmailClassifier.Classify("Newsletter", "Check out our latest blog post.");

        result.SuggestedStatus.ShouldBeNull();
        result.MatchedRule.ShouldBe("NoMatch");
        result.ConfidenceScore.ShouldBe(0);
    }

    [Fact]
    public void Classify_Conflicting_Phrases_Rejection_Wins_Over_Interview()
    {
        var result = RuleBasedEmailClassifier.Classify("Update",
            "Unfortunately, after the interview we have decided to move forward with other candidates.");

        result.SuggestedStatus.ShouldBe(ApplicationStatus.Rejected);
        result.MatchedRule.ShouldBe("Rejection");
    }

    [Fact]
    public void Classify_Multiple_Phrases_From_Same_Rule_Increases_Confidence()
    {
        var single = RuleBasedEmailClassifier.Classify("", "Unfortunately, we won't be proceeding.");
        var multiple = RuleBasedEmailClassifier.Classify("",
            "Unfortunately, we have decided to move forward with other candidates and will not be moving forward with your application.");

        multiple.ConfidenceScore.ShouldBeGreaterThan(single.ConfidenceScore);
    }

    [Fact]
    public void Classify_Negated_Interview_Invitation_Does_Not_Suggest_Interview()
    {
        // Real example: a rejection that mentions "interview" inside a negated clause
        // ("will not invite you FOR an interview") — dangerously close to the InterviewInvitation
        // rule's "invite you TO an interview" phrase. Must not false-positive as Interview.
        var result = RuleBasedEmailClassifier.Classify("Your application",
            "Unfortunately, we will not invite you for an interview, because we are looking for " +
            "candidates who already live in the Netherlands.");

        result.SuggestedStatus.ShouldNotBe(ApplicationStatus.Interview);
    }
}
