using AfterApply.Application.Notifications.Contracts;
using AfterApply.Application.Notifications.Validators;
using Shouldly;

namespace AfterApply.UnitTests.Notifications;

public class ReminderRequestValidatorTests
{
    private static readonly Guid FirstId = Guid.NewGuid();

    private static readonly BulkReminderRequestValidator Validator = new();

    [Fact]
    public void An_Explicit_Id_List_Needs_No_Expected_Count()
    {
        Validator.Validate(new BulkReminderRequest(new ReminderSelection(Ids: [FirstId]))).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void An_All_Selection_Must_State_The_Count_It_Was_Shown()
    {
        var result = Validator.Validate(new BulkReminderRequest(new ReminderSelection(All: true)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(BulkReminderRequest.ExpectedCount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1224)]
    public void An_All_Selection_With_A_Count_Is_Accepted(int expectedCount)
    {
        Validator.Validate(new BulkReminderRequest(new ReminderSelection(All: true), expectedCount)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Naming_Both_Forms_Or_Neither_Is_Rejected()
    {
        Validator.Validate(new BulkReminderRequest(new ReminderSelection(Ids: [FirstId], All: true), 1)).IsValid.ShouldBeFalse();
        Validator.Validate(new BulkReminderRequest(new ReminderSelection())).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void An_Empty_Or_Repeating_Id_List_Is_Rejected()
    {
        Validator.Validate(new BulkReminderRequest(new ReminderSelection(Ids: []))).IsValid.ShouldBeFalse();
        Validator.Validate(new BulkReminderRequest(new ReminderSelection(Ids: [FirstId, FirstId]))).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void An_Id_List_Wider_Than_A_Page_Could_Ever_Be_Is_Rejected()
    {
        // The id form is what one page's checkboxes produce; anything wider is what "all" is for.
        var ids = Enumerable.Range(0, BulkReminderRequestValidator.MaxIds + 1).Select(_ => Guid.NewGuid()).ToList();

        Validator.Validate(new BulkReminderRequest(new ReminderSelection(Ids: ids))).IsValid.ShouldBeFalse();
        Validator.Validate(new BulkReminderRequest(new ReminderSelection(Ids: ids.Take(BulkReminderRequestValidator.MaxIds).ToList())))
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void The_List_Query_Keeps_The_Page_Size_Under_The_Ceiling()
    {
        var validator = new GetRemindersQueryValidator();

        validator.Validate(new GetRemindersQuery()).IsValid.ShouldBeTrue();
        validator.Validate(new GetRemindersQuery(Page: 0)).IsValid.ShouldBeFalse();
        validator.Validate(new GetRemindersQuery(PageSize: 0)).IsValid.ShouldBeFalse();
        validator.Validate(new GetRemindersQuery(PageSize: 51)).IsValid.ShouldBeFalse();
        validator.Validate(new GetRemindersQuery(Page: 245, PageSize: 50)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(14, true)]
    [InlineData(30, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(-7, false)]
    [InlineData(31, false)]
    [InlineData(365, false)]
    public void A_Break_Is_A_Week_A_Fortnight_Or_A_Month_And_Nothing_Else(int days, bool valid)
    {
        // The profile card offers three lengths and the API accepts exactly those — a year-long
        // "break" typed into a request body is not a break, it is turning the feature off.
        new PauseRemindersRequestValidator().Validate(new PauseRemindersRequest(days)).IsValid.ShouldBe(valid);
    }
}
