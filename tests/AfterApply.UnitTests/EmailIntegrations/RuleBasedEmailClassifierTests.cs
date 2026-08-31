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
