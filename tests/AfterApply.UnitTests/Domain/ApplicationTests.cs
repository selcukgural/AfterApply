using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
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
    public void Create_Sets_Status_To_Applied_And_Seeds_History_And_Timeline()
    {
        var application = CreateApplication();

        application.Status.ShouldBe(ApplicationStatus.Applied);
        application.StatusHistory.Count.ShouldBe(1);
        application.StatusHistory.Single().FromStatus.ShouldBeNull();
        application.StatusHistory.Single().ToStatus.ShouldBe(ApplicationStatus.Applied);
        application.Events.Count.ShouldBe(1);
        application.Events.Single().Type.ShouldBe(ApplicationEventType.ApplicationCreated);
    }

    [Fact]
    public void ChangeStatus_Records_History_And_Event()
    {
        var application = CreateApplication();
        var changedAt = Now.AddDays(3);

        application.ChangeStatus(ApplicationStatus.Screening, changedAt, Source.Manual, note: "Recruiter reached out");

        application.Status.ShouldBe(ApplicationStatus.Screening);
        application.UpdatedAt.ShouldBe(changedAt);
        application.StatusHistory.Count.ShouldBe(2);
        var lastHistory = application.StatusHistory.Last();
        lastHistory.FromStatus.ShouldBe(ApplicationStatus.Applied);
        lastHistory.ToStatus.ShouldBe(ApplicationStatus.Screening);
        application.Events.Count.ShouldBe(2);
        application.Events.Last().Type.ShouldBe(ApplicationEventType.StatusChanged);
    }

    [Fact]
    public void ChangeStatus_To_Same_Status_Throws()
    {
        var application = CreateApplication();

        Should.Throw<ApplicationAlreadyInStatusException>(() =>
            application.ChangeStatus(ApplicationStatus.Applied, Now.AddDays(1), Source.Manual, null));
    }

    [Fact]
    public void ChangeStatus_Allows_NonLinear_Transitions()
    {
        var application = CreateApplication();

        // Real hiring pipelines aren't linear: rejection can happen straight
        // from Applied, skipping Screening/Interview entirely.
        application.ChangeStatus(ApplicationStatus.Rejected, Now.AddDays(1), Source.Manual, null);

        application.Status.ShouldBe(ApplicationStatus.Rejected);
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
}
