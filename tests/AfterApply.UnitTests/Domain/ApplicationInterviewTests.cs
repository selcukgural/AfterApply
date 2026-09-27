using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using Shouldly;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.UnitTests.Domain;

public class ApplicationInterviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Interview = new(2026, 9, 28, 14, 0, 0, TimeSpan.FromHours(3));

    private static DomainApplication CreateApplication(ApplicationStatus status = ApplicationStatus.Interview)
    {
        var application = DomainApplication.Create(
            userId: Guid.CreateVersion7(),
            companyId: Guid.CreateVersion7(),
            jobTitle: "Backend Developer",
            jobUrl: null,
            location: null,
            employmentType: EmploymentType.FullTime,
            appliedAt: Now.AddDays(-10),
            source: Source.Manual,
            notes: null,
            now: Now.AddDays(-10));
        if (status != ApplicationStatus.Applied)
        {
            application.ChangeStatus(status, Now.AddDays(-1), StatusChangeContext.Manual());
        }

        return application;
    }

    [Fact]
    public void SetInterview_Records_The_Instant_In_Utc_The_Format_And_The_Stage()
    {
        var application = CreateApplication();

        application.SetInterview(Interview, InterviewFormat.Phone, Now);

        application.InterviewAt.ShouldBe(Interview);
        application.InterviewAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
        application.InterviewFormat.ShouldBe(InterviewFormat.Phone);
        application.InterviewStatus.ShouldBe(ApplicationStatus.Interview);
        application.CurrentInterviewAt.ShouldBe(Interview);
        application.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public void SetInterview_Without_A_Format_Defaults_To_Online()
    {
        var application = CreateApplication();

        application.SetInterview(Interview, null, Now);

        application.InterviewFormat.ShouldBe(InterviewFormat.Online);
    }

    [Theory]
    [InlineData(ApplicationStatus.Applied)]
    [InlineData(ApplicationStatus.Offer)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Ghosted)]
    public void SetInterview_Outside_An_Interview_Stage_Is_Refused(ApplicationStatus status)
    {
        var application = CreateApplication(status);

        Should.Throw<InterviewOutsideInterviewStageException>(() => application.SetInterview(Interview, null, Now));
    }

    [Fact]
    public void SetInterview_Null_Clears_Everything_Even_On_A_Closed_Application()
    {
        var application = CreateApplication();
        application.SetInterview(Interview, InterviewFormat.InPerson, Now);
        application.ChangeStatus(ApplicationStatus.Rejected, Now.AddDays(2), StatusChangeContext.Manual());

        application.SetInterview(null, null, Now.AddDays(3));

        application.InterviewAt.ShouldBeNull();
        application.InterviewFormat.ShouldBeNull();
        application.InterviewStatus.ShouldBeNull();
    }

    [Fact]
    public void A_New_Or_Moved_Date_Is_An_InterviewScheduled_Event_But_A_Format_Change_Is_Not()
    {
        var application = CreateApplication();
        int Scheduled() => application.Events.Count(e => e.Type == ApplicationEventType.InterviewScheduled);

        application.SetInterview(Interview, InterviewFormat.Online, Now);
        Scheduled().ShouldBe(1);

        application.SetInterview(Interview, InterviewFormat.InPerson, Now);
        Scheduled().ShouldBe(1);

        application.SetInterview(Interview.AddHours(2), InterviewFormat.InPerson, Now);
        Scheduled().ShouldBe(2);
    }

    [Fact]
    public void Moving_On_Hides_The_Interview_And_Undoing_The_Move_Brings_It_Back()
    {
        var application = CreateApplication();
        application.SetInterview(Interview, null, Now);

        application.ChangeStatus(ApplicationStatus.FinalInterview, Now.AddDays(2), StatusChangeContext.Manual());
        application.CurrentInterviewAt.ShouldBeNull();

        application.ChangeStatus(ApplicationStatus.Interview, Now.AddDays(2), StatusChangeContext.Manual());
        application.CurrentInterviewAt.ShouldBe(Interview);
    }

    [Fact]
    public void Screening_Can_Hold_An_Interview()
    {
        var application = CreateApplication(ApplicationStatus.Screening);

        application.SetInterview(Interview, InterviewFormat.Phone, Now);

        application.CurrentInterviewAt.ShouldBe(Interview);
    }
}
