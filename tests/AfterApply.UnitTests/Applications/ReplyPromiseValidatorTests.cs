using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Applications.Validators;
using AfterApply.Application.Localization;
using AfterApply.Domain.Applications;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.Applications;

public class ReplyPromiseValidatorTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly ChangeStatusRequestValidator _changeStatus = new(new KeyEchoLocalizer());
    private readonly SetReplyPromiseRequestValidator _setPromise = new(new KeyEchoLocalizer());

    [Fact]
    public void A_Status_Change_Without_The_New_Fields_Is_Valid_As_It_Always_Was()
    {
        _changeStatus.Validate(new ChangeStatusRequest(ApplicationStatus.Interview, null, null)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ApplicationStatus.Applied)]
    [InlineData(ApplicationStatus.Screening)]
    [InlineData(ApplicationStatus.Interview)]
    [InlineData(ApplicationStatus.TechnicalInterview)]
    [InlineData(ApplicationStatus.FinalInterview)]
    [InlineData(ApplicationStatus.Offer)]
    public void A_Reply_Date_Is_Accepted_With_A_Status_Still_In_Play(ApplicationStatus status)
    {
        _changeStatus.Validate(new ChangeStatusRequest(status, null, null, Today.AddDays(7))).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.Withdrawn)]
    [InlineData(ApplicationStatus.Ghosted)]
    public void A_Reply_Date_Is_Refused_With_A_Closing_Status(ApplicationStatus status)
    {
        var result = _changeStatus.Validate(new ChangeStatusRequest(status, null, null, Today.AddDays(7)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == "VALIDATION_REPLY_PROMISE_ON_CLOSING_STATUS");
    }

    [Fact]
    public void How_The_Rejection_Was_Learned_Is_Accepted_Only_With_Rejected()
    {
        _changeStatus.Validate(new ChangeStatusRequest(ApplicationStatus.Rejected, null, null, null,
            RejectionNotice.SeenOnPortal)).IsValid.ShouldBeTrue();

        var result = _changeStatus.Validate(new ChangeStatusRequest(ApplicationStatus.Offer, null, null, null,
            RejectionNotice.SeenOnPortal));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == "VALIDATION_REJECTION_NOTICE_NOT_REJECTED");
    }

    [Fact]
    public void An_Undefined_Rejection_Notice_Is_Refused()
    {
        _changeStatus.Validate(new ChangeStatusRequest(ApplicationStatus.Rejected, null, null, null,
            (RejectionNotice)42)).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(-365, true)]
    [InlineData(-366, false)]
    [InlineData(0, true)]
    [InlineData(365, true)]
    [InlineData(366, false)]
    public void The_Date_Must_Be_Within_A_Year_Either_Side_Of_Today(int offsetDays, bool valid)
    {
        var date = Today.AddDays(offsetDays);

        _setPromise.Validate(new SetReplyPromiseRequest(date)).IsValid.ShouldBe(valid);
        _changeStatus.Validate(new ChangeStatusRequest(ApplicationStatus.Interview, null, null, date)).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Clearing_The_Date_Is_Always_Valid()
    {
        _setPromise.Validate(new SetReplyPromiseRequest(null)).IsValid.ShouldBeTrue();
    }

    // Same shape the identity validator tests use — there is no mocking library in this project.
    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
