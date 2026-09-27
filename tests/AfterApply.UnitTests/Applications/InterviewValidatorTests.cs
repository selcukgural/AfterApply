using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Applications.Validators;
using AfterApply.Application.Localization;
using AfterApply.Application.Notifications.Contracts;
using AfterApply.Application.Notifications.Validators;
using AfterApply.Domain.Applications;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.Applications;

public class InterviewValidatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly ChangeStatusRequestValidator _changeStatus = new(new KeyEchoLocalizer());
    private readonly SetInterviewRequestValidator _setInterview = new(new KeyEchoLocalizer());
    private readonly InterviewOutcomeRequestValidator _outcome = new(new KeyEchoLocalizer());
    private readonly SnoozeReminderRequestValidator _snooze = new();
    private readonly UndoInterviewOutcomeRequestValidator _undo = new();

    [Theory]
    [InlineData(ApplicationStatus.Screening)]
    [InlineData(ApplicationStatus.Interview)]
    [InlineData(ApplicationStatus.TechnicalInterview)]
    [InlineData(ApplicationStatus.FinalInterview)]
    public void A_Status_Change_Into_An_Interview_Stage_Can_Carry_The_Interview(ApplicationStatus status)
    {
        var request = new ChangeStatusRequest(status, null, null, InterviewAt: Now.AddDays(2), InterviewFormat: InterviewFormat.Online);

        _changeStatus.Validate(request).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ApplicationStatus.Applied)]
    [InlineData(ApplicationStatus.Offer)]
    [InlineData(ApplicationStatus.Rejected)]
    public void A_Status_Change_Elsewhere_Cannot_Carry_One(ApplicationStatus status)
    {
        var result = _changeStatus.Validate(new ChangeStatusRequest(status, null, null, InterviewAt: Now.AddDays(2)));

        result.Errors.ShouldContain(e => e.ErrorMessage == "INTERVIEW_OUTSIDE_INTERVIEW_STAGE");
    }

    [Theory]
    [InlineData(-29, true)]
    [InlineData(-31, false)]
    [InlineData(364, true)]
    [InlineData(366, false)]
    public void The_Interview_Date_Is_Held_To_A_Month_Back_And_A_Year_Ahead(int days, bool valid)
    {
        _setInterview.Validate(new SetInterviewRequest(Now.AddDays(days))).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Clearing_The_Interview_Is_Always_Valid()
    {
        _setInterview.Validate(new SetInterviewRequest(null)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void An_Unknown_Format_Is_Refused()
    {
        _setInterview.Validate(new SetInterviewRequest(Now.AddDays(1), (InterviewFormat)42)).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(ApplicationStatus.Interview, true)]
    [InlineData(ApplicationStatus.TechnicalInterview, true)]
    [InlineData(ApplicationStatus.FinalInterview, true)]
    [InlineData(ApplicationStatus.Offer, true)]
    [InlineData(ApplicationStatus.Accepted, true)]
    [InlineData(ApplicationStatus.Screening, false)]
    [InlineData(ApplicationStatus.Rejected, false)]
    [InlineData(ApplicationStatus.Applied, false)]
    public void Next_Stage_Names_A_Stage_After_An_Interview(ApplicationStatus next, bool valid)
    {
        _outcome.Validate(new InterviewOutcomeRequest(InterviewOutcome.NextStage, next)).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Next_Stage_Without_A_Stage_Is_Refused()
    {
        _outcome.Validate(new InterviewOutcomeRequest(InterviewOutcome.NextStage)).Errors
            .ShouldContain(e => e.ErrorMessage == "VALIDATION_INTERVIEW_NEXT_STATUS");
    }

    [Fact]
    public void Waiting_Takes_A_Reply_Date_Or_None()
    {
        _outcome.Validate(new InterviewOutcomeRequest(InterviewOutcome.Waiting)).IsValid.ShouldBeTrue();
        _outcome.Validate(new InterviewOutcomeRequest(InterviewOutcome.Waiting, PromisedReplyBy: Today.AddDays(7))).IsValid.ShouldBeTrue();
        _outcome.Validate(new InterviewOutcomeRequest(InterviewOutcome.Waiting, PromisedReplyBy: Today.AddDays(400))).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Only_Waiting_Takes_A_Date_And_Only_Next_Stage_Takes_A_Status()
    {
        _outcome.Validate(new InterviewOutcomeRequest(InterviewOutcome.Rejected, PromisedReplyBy: Today)).IsValid.ShouldBeFalse();
        _outcome.Validate(new InterviewOutcomeRequest(InterviewOutcome.Waiting, ApplicationStatus.Offer)).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(7, true)]
    [InlineData(14, true)]
    [InlineData(2, false)]
    [InlineData(30, false)]
    [InlineData(0, false)]
    public void Snooze_Takes_Only_The_Lengths_The_Menu_Offers(int days, bool valid)
    {
        _snooze.Validate(new SnoozeReminderRequest(days)).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void An_Undo_Names_Both_Ends_Of_A_Status_Move_Or_Neither()
    {
        _undo.Validate(new UndoInterviewOutcomeRequest(ApplicationStatus.Interview, ApplicationStatus.Offer, null)).IsValid.ShouldBeTrue();
        _undo.Validate(new UndoInterviewOutcomeRequest(null, null, Today)).IsValid.ShouldBeTrue();
        _undo.Validate(new UndoInterviewOutcomeRequest(ApplicationStatus.Interview, null, null)).IsValid.ShouldBeFalse();
        _undo.Validate(new UndoInterviewOutcomeRequest(null, ApplicationStatus.Offer, null)).IsValid.ShouldBeFalse();
    }

    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
