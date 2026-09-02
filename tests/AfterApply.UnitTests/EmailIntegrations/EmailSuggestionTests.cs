using AfterApply.Domain.Applications;
using AfterApply.Domain.EmailIntegrations;
using Shouldly;

namespace AfterApply.UnitTests.EmailIntegrations;

public class EmailSuggestionTests
{
    [Fact]
    public void Create_Sets_ApplicationId_And_Leaves_Extracted_Fields_Null()
    {
        var now = DateTimeOffset.UtcNow;
        var applicationId = Guid.NewGuid();

        var suggestion = EmailSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), applicationId,
            "msg-1", "thread-1", ApplicationStatus.Interview, 0.85, "InterviewInvitation",
            EmailApplicationMatchType.DomainMatch, "acme.com", now, now);

        suggestion.ApplicationId.ShouldBe(applicationId);
        suggestion.Status.ShouldBe(EmailSuggestionStatus.Pending);
        suggestion.ExtractedCompanyName.ShouldBeNull();
        suggestion.ExtractedJobTitle.ShouldBeNull();
    }

    [Fact]
    public void Create_Persists_MatchType()
    {
        var now = DateTimeOffset.UtcNow;
        var applicationId = Guid.NewGuid();

        var suggestion = EmailSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), applicationId,
            "msg-1", "thread-1", ApplicationStatus.Interview, 0.85, "InterviewInvitation",
            EmailApplicationMatchType.NameFallbackMatch, "acme.com", now, now);

        suggestion.MatchType.ShouldBe(EmailApplicationMatchType.NameFallbackMatch);
    }

    [Fact]
    public void CreateForNewJob_Leaves_ApplicationId_Null_And_Persists_Extracted_Fields()
    {
        var now = DateTimeOffset.UtcNow;

        var suggestion = EmailSuggestion.CreateForNewJob(Guid.NewGuid(), Guid.NewGuid(), "msg-1",
            ApplicationStatus.Interview, 0.85, "InterviewInvitation", "acme.com", now, now,
            "Interview invitation", "We'd like to invite you to an interview.",
            "Acme Corp", "Backend Engineer", "Istanbul", "Build backend services.");

        suggestion.ApplicationId.ShouldBeNull();
        suggestion.Status.ShouldBe(EmailSuggestionStatus.Pending);
        suggestion.ExtractedCompanyName.ShouldBe("Acme Corp");
        suggestion.ExtractedJobTitle.ShouldBe("Backend Engineer");
        suggestion.ExtractedLocation.ShouldBe("Istanbul");
        suggestion.ExtractedDescription.ShouldBe("Build backend services.");
        suggestion.SuggestedStatus.ShouldBe(ApplicationStatus.Interview);
        suggestion.MatchType.ShouldBeNull();
    }

    [Fact]
    public void Confirm_Sets_Status_Regardless_Of_Suggestion_Kind()
    {
        var now = DateTimeOffset.UtcNow;
        var suggestion = EmailSuggestion.CreateForNewJob(Guid.NewGuid(), Guid.NewGuid(), "msg-1",
            null, 0.5, "StillWaiting", "acme.com", now, now, "Update", "Still under review.",
            "Acme Corp", "Backend Engineer", null, null);

        suggestion.Confirm(now.AddMinutes(1));

        suggestion.Status.ShouldBe(EmailSuggestionStatus.Confirmed);
        suggestion.ResolvedAt.ShouldBe(now.AddMinutes(1));
    }

    [Fact]
    public void AutoApply_Sets_Status_To_AutoApplied_And_ResolvedAt()
    {
        var now = DateTimeOffset.UtcNow;
        var applicationId = Guid.NewGuid();
        var suggestion = EmailSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), applicationId,
            "msg-1", "thread-1", ApplicationStatus.Rejected, 0.95, "Llm:Rejected",
            EmailApplicationMatchType.DomainMatch, "acme.com", now, now);

        suggestion.AutoApply(now.AddMinutes(1));

        suggestion.Status.ShouldBe(EmailSuggestionStatus.AutoApplied);
        suggestion.ResolvedAt.ShouldBe(now.AddMinutes(1));
    }

    [Fact]
    public void MarkRead_Sets_IsRead_And_ReadAtUtc()
    {
        var now = DateTimeOffset.UtcNow;
        var suggestion = EmailSuggestion.CreateForNewJob(Guid.NewGuid(), Guid.NewGuid(), "msg-1",
            null, 0.5, "StillWaiting", "acme.com", now, now, "Update", "Still under review.",
            "Acme Corp", "Backend Engineer", null, null);

        suggestion.IsRead.ShouldBeFalse();

        suggestion.MarkRead(now.AddMinutes(1));

        suggestion.IsRead.ShouldBeTrue();
        suggestion.ReadAtUtc.ShouldBe(now.AddMinutes(1));
    }
}
