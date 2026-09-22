using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.EmailIntegrations;
using Shouldly;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.UnitTests.Domain;

public class ApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private static DomainApplication CreateApplication() => DomainApplication.Create(
        userId: Guid.CreateVersion7(),
        companyId: Guid.CreateVersion7(),
        jobTitle: "Senior Backend Engineer",
        jobUrl: null,
        location: null,
        employmentType: EmploymentType.FullTime,
        appliedAt: Now,
        source: Source.Manual,
        notes: null,
        now: Now);

    [Fact]
    public void SetHrEmailFromIncomingEmail_Fills_An_Empty_Contact_And_Records_Where_It_Came_From()
    {
        var application = CreateApplication();

        application.SetHrEmailFromIncomingEmail("ayse.yilmaz@company.com", Now.AddDays(1));

        application.HrEmail.ShouldBe("ayse.yilmaz@company.com");
        application.HrEmailSource.ShouldBe(HrEmailSource.IncomingEmail);
        application.UpdatedAt.ShouldBe(Now.AddDays(1));
    }

    [Fact]
    public void SetHrEmailFromIncomingEmail_Never_Overwrites_What_The_User_Typed()
    {
        // A guess read off a sender must not quietly replace an address the user asserted, however
        // strong the match that produced it.
        var application = CreateApplication();
        application.UpdateDetails("Senior Backend Engineer", null, null, EmploymentType.FullTime, Now, null, Now,
            hrEmail: "mine@company.com");

        application.SetHrEmailFromIncomingEmail("sender@company.com", Now.AddDays(1));

        application.HrEmail.ShouldBe("mine@company.com");
        application.HrEmailSource.ShouldBe(HrEmailSource.Manual);
    }

    [Fact]
    public void Saving_The_Edit_Form_Makes_An_Auto_Filled_Address_The_Users_Own()
    {
        var application = CreateApplication();
        application.SetHrEmailFromIncomingEmail("sender@company.com", Now);

        application.UpdateDetails("Senior Backend Engineer", null, null, EmploymentType.FullTime, Now, null, Now,
            hrEmail: "sender@company.com");

        application.HrEmailSource.ShouldBe(HrEmailSource.Manual);
    }

    [Fact]
    public void Clearing_The_Hr_Email_Clears_Its_Provenance_Too()
    {
        var application = CreateApplication();
        application.SetHrEmailFromIncomingEmail("sender@company.com", Now);

        application.UpdateDetails("Senior Backend Engineer", null, null, EmploymentType.FullTime, Now, null, Now);

        application.HrEmail.ShouldBeNull();
        application.HrEmailSource.ShouldBeNull();
    }

    [Fact]
    public void Create_Sets_Status_To_Applied_And_Seeds_History_And_Timeline()
    {
        var application = CreateApplication();

        application.Status.ShouldBe(ApplicationStatus.Applied);
        application.StatusHistory.Count.ShouldBe(1);
        application.StatusHistory.Single().FromStatus.ShouldBeNull();
        application.StatusHistory.Single().ToStatus.ShouldBe(ApplicationStatus.Applied);
        application.StatusHistory.Single().Origin.ShouldBe(StatusChangeOrigin.Manual);
        application.StatusHistory.Single().Source.ShouldBe(Source.Manual);
        application.StatusHistory.Single().ChangedAt.ShouldBe(Now);
        application.Events.Count.ShouldBe(1);
        application.Events.Single().Type.ShouldBe(ApplicationEventType.ApplicationCreated);
    }

    [Fact]
    public void Create_Stamps_The_Seed_History_Row_With_AppliedAt_Not_Now()
    {
        // A backdated entry — the user applied last week and is only recording it today, or an
        // import is replaying an old CSV row. History is ordered by ChangedAt, so the row that
        // says "became Applied" has to carry the date that actually happened.
        var appliedAt = Now.AddDays(-30);
        var application = DomainApplication.Create(
            userId: Guid.CreateVersion7(),
            companyId: Guid.CreateVersion7(),
            jobTitle: "Senior Backend Engineer",
            jobUrl: null,
            location: null,
            employmentType: EmploymentType.FullTime,
            appliedAt: appliedAt,
            source: Source.CsvImport,
            notes: null,
            now: Now);

        application.StatusHistory.Single().ChangedAt.ShouldBe(appliedAt);
        application.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void ChangeStatus_Records_History_And_Event()
    {
        var application = CreateApplication();
        var changedAt = Now.AddDays(3);

        application.ChangeStatus(ApplicationStatus.Screening, changedAt,
            StatusChangeContext.Manual("Recruiter reached out"));

        application.Status.ShouldBe(ApplicationStatus.Screening);
        application.UpdatedAt.ShouldBe(changedAt);
        application.StatusHistory.Count.ShouldBe(2);
        var lastHistory = application.StatusHistory.Last();
        lastHistory.FromStatus.ShouldBe(ApplicationStatus.Applied);
        lastHistory.ToStatus.ShouldBe(ApplicationStatus.Screening);
        lastHistory.Note.ShouldBe("Recruiter reached out");
        lastHistory.Origin.ShouldBe(StatusChangeOrigin.Manual);
        lastHistory.Source.ShouldBe(Source.Manual);
        lastHistory.EmailSuggestionId.ShouldBeNull();
        application.Events.Count.ShouldBe(2);
        application.Events.Last().Type.ShouldBe(ApplicationEventType.StatusChanged);
    }

    [Fact]
    public void ChangeStatus_To_Same_Status_Throws()
    {
        var application = CreateApplication();

        Should.Throw<ApplicationAlreadyInStatusException>(() =>
            application.ChangeStatus(ApplicationStatus.Applied, Now.AddDays(1), StatusChangeContext.Manual()));
    }

    [Fact]
    public void ChangeStatus_Allows_NonLinear_Transitions()
    {
        var application = CreateApplication();

        // Real hiring pipelines aren't linear: rejection can happen straight
        // from Applied, skipping Screening/Interview entirely.
        application.ChangeStatus(ApplicationStatus.Rejected, Now.AddDays(1), StatusChangeContext.Manual());

        application.Status.ShouldBe(ApplicationStatus.Rejected);
    }

    [Fact]
    public void Create_From_Import_Seeds_History_With_Import_Origin()
    {
        // The seed "→ Applied" row has no StatusChanged event to borrow provenance from, so
        // Create() has to derive it from the Source the caller came in with.
        var application = DomainApplication.Create(
            userId: Guid.CreateVersion7(),
            companyId: Guid.CreateVersion7(),
            jobTitle: "Senior Backend Engineer",
            jobUrl: null,
            location: null,
            employmentType: EmploymentType.FullTime,
            appliedAt: Now,
            source: Source.LinkedInImport,
            notes: null,
            now: Now);

        application.StatusHistory.Single().Origin.ShouldBe(StatusChangeOrigin.Import);
        application.StatusHistory.Single().Source.ShouldBe(Source.LinkedInImport);
    }

    [Fact]
    public void ChangeStatus_Records_Email_Provenance_As_Structured_Fields()
    {
        var application = CreateApplication();
        var suggestionId = Guid.CreateVersion7();

        application.ChangeStatus(ApplicationStatus.Rejected, Now.AddDays(4), new StatusChangeContext(
            Source.Email, StatusChangeOrigin.EmailAutoApplied, Note: null, suggestionId,
            RejectionReasonCategory.LocationOrRelocation, "candidates already based in NL"));

        var history = application.StatusHistory.Last();
        history.Origin.ShouldBe(StatusChangeOrigin.EmailAutoApplied);
        history.EmailSuggestionId.ShouldBe(suggestionId);
        history.RejectionReasonCategory.ShouldBe(RejectionReasonCategory.LocationOrRelocation);
        history.RejectionReasonDetail.ShouldBe("candidates already based in NL");

        // The note stays the user's own field — the reason is carried structurally so the UI can
        // render it in the viewer's language instead of a baked-in Turkish sentence.
        history.Note.ShouldBeNull();

        // Source.Email alone cannot tell a confirmed suggestion from an auto-applied one; that is
        // exactly why Origin exists alongside it.
        history.Source.ShouldBe(Source.Email);
    }

    [Fact]
    public void AddEvent_Rejects_StatusChanged_Type()
    {
        var application = CreateApplication();

        Should.Throw<StatusChangedEventNotAllowedException>(() =>
            application.AddEvent(ApplicationEventType.StatusChanged, Now.AddDays(1), Source.Manual, null));
    }

    [Fact]
    public void AddEvent_Appends_To_Timeline()
    {
        var application = CreateApplication();

        application.AddEvent(ApplicationEventType.RecruiterContacted, Now.AddDays(2), Source.Email, metadata: null);

        application.Events.Count.ShouldBe(2);
        application.Events.Last().Type.ShouldBe(ApplicationEventType.RecruiterContacted);
    }
    [Fact]
    public void SetReplyPromise_Records_The_Date_And_The_Stage_It_Was_Given_In()
    {
        var application = CreateApplication();
        application.ChangeStatus(ApplicationStatus.Interview, Now.AddDays(3), StatusChangeContext.Manual());

        application.SetReplyPromise(new DateOnly(2026, 9, 5), Now.AddDays(3), Now.AddDays(4));

        application.PromisedReplyBy.ShouldBe(new DateOnly(2026, 9, 5));
        application.PromisedReplyStatus.ShouldBe(ApplicationStatus.Interview);
        application.PromisedReplySince.ShouldBe(Now.AddDays(3));
        application.UpdatedAt.ShouldBe(Now.AddDays(4));
    }

    [Fact]
    public void SetReplyPromise_Moving_The_Date_In_The_Same_Stage_Keeps_The_Stage()
    {
        var application = CreateApplication();
        application.SetReplyPromise(new DateOnly(2026, 9, 1), Now, Now);

        // "They pushed it to Friday": same promise, new date.
        application.SetReplyPromise(new DateOnly(2026, 9, 4), Now, Now.AddDays(2));

        application.PromisedReplyBy.ShouldBe(new DateOnly(2026, 9, 4));
        application.PromisedReplyStatus.ShouldBe(ApplicationStatus.Applied);
        application.PromisedReplySince.ShouldBe(Now);
    }

    [Fact]
    public void SetReplyPromise_In_A_Later_Stage_Replaces_The_Earlier_Promise()
    {
        var application = CreateApplication();
        application.SetReplyPromise(new DateOnly(2026, 9, 1), Now, Now);
        application.ChangeStatus(ApplicationStatus.Screening, Now.AddDays(5), StatusChangeContext.Manual());

        application.SetReplyPromise(new DateOnly(2026, 9, 12), Now.AddDays(5), Now.AddDays(5));

        application.PromisedReplyStatus.ShouldBe(ApplicationStatus.Screening);
        application.PromisedReplySince.ShouldBe(Now.AddDays(5));
    }

    [Fact]
    public void SetReplyPromise_Null_Clears_All_Three_Fields()
    {
        var application = CreateApplication();
        application.SetReplyPromise(new DateOnly(2026, 9, 1), Now, Now);

        application.SetReplyPromise(null, Now, Now.AddDays(1));

        application.PromisedReplyBy.ShouldBeNull();
        application.PromisedReplyStatus.ShouldBeNull();
        application.PromisedReplySince.ShouldBeNull();
    }

    [Theory]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.Withdrawn)]
    [InlineData(ApplicationStatus.Ghosted)]
    public void SetReplyPromise_Refuses_A_Closed_Application_But_Still_Lets_It_Be_Cleared(ApplicationStatus closed)
    {
        var application = CreateApplication();
        application.SetReplyPromise(new DateOnly(2026, 9, 1), Now, Now);
        application.ChangeStatus(closed, Now.AddDays(1), StatusChangeContext.Manual());

        Should.Throw<ReplyPromiseOnClosedApplicationException>(() =>
            application.SetReplyPromise(new DateOnly(2026, 9, 9), Now.AddDays(1), Now.AddDays(1)));

        application.SetReplyPromise(null, Now, Now.AddDays(2));
        application.PromisedReplyBy.ShouldBeNull();
    }

    [Fact]
    public void ChangeStatus_To_Rejected_Keeps_What_The_User_Said_About_How_They_Learned()
    {
        var application = CreateApplication();

        application.ChangeStatus(ApplicationStatus.Rejected, Now.AddDays(1), StatusChangeContext.Manual(),
            RejectionNotice.SeenOnPortal);

        application.RejectionNotice.ShouldBe(RejectionNotice.SeenOnPortal);
    }

    [Theory]
    [InlineData(StatusChangeOrigin.EmailSuggestionConfirmed)]
    [InlineData(StatusChangeOrigin.EmailAutoApplied)]
    public void ChangeStatus_To_Rejected_From_The_Companys_Email_Is_CompanyNotified_Without_Asking(StatusChangeOrigin origin)
    {
        var application = CreateApplication();

        application.ChangeStatus(ApplicationStatus.Rejected, Now.AddDays(1), new StatusChangeContext(Source.Email, origin));

        application.RejectionNotice.ShouldBe(RejectionNotice.CompanyNotified);
    }

    [Fact]
    public void ChangeStatus_Away_From_Rejected_Clears_The_Answer_And_A_Non_Rejection_Never_Takes_One()
    {
        var application = CreateApplication();
        application.ChangeStatus(ApplicationStatus.Rejected, Now.AddDays(1), StatusChangeContext.Manual(),
            RejectionNotice.CompanyNotified);

        application.ChangeStatus(ApplicationStatus.Interview, Now.AddDays(2), StatusChangeContext.Manual(),
            RejectionNotice.CompanyNotified);

        application.RejectionNotice.ShouldBeNull();
    }
}
