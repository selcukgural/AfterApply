using AfterApply.Application.EmailIntegrations.Contracts;
using AfterApply.Application.Notifications;
using AfterApply.Application.Notifications.Contracts;
using AfterApply.Application.Notifications.Validators;
using AfterApply.Domain.Notifications;
using AfterApply.Infrastructure.Notifications;
using Shouldly;

namespace AfterApply.UnitTests.Notifications;

public class ContributionNotificationTests
{
    [Theory]
    // 20:59 UTC on the 22nd is 23:59 in Istanbul — still the 22nd.
    [InlineData("2026-09-22T20:59:00Z", "2026-09-22")]
    // 21:00 UTC on the 22nd is midnight in Istanbul — the 23rd already, whatever UTC says.
    [InlineData("2026-09-22T21:00:00Z", "2026-09-23")]
    [InlineData("2026-09-23T12:00:00Z", "2026-09-23")]
    public void DayOf_Uses_The_Istanbul_Calendar(string at, string expected) =>
        ContributionNotification.DayOf(DateTimeOffset.Parse(at)).ShouldBe(DateOnly.Parse(expected));

    [Theory]
    [InlineData(ContributionNotificationType.ReviewHelpful)]
    [InlineData(ContributionNotificationType.SalaryHelpful)]
    [InlineData(ContributionNotificationType.ExperienceHelpful)]
    [InlineData(ContributionNotificationType.BlogCommentHelpful)]
    public void Every_Kind_Is_Allowed_When_Everything_Is_On(ContributionNotificationType type) =>
        NotificationPreferenceRules.Allows(type, true, true, true, true, true).ShouldBeTrue();

    [Theory]
    [InlineData(ContributionNotificationType.ReviewHelpful)]
    [InlineData(ContributionNotificationType.SalaryHelpful)]
    [InlineData(ContributionNotificationType.ExperienceHelpful)]
    [InlineData(ContributionNotificationType.BlogCommentHelpful)]
    public void The_Master_Switch_Silences_Every_Kind(ContributionNotificationType type) =>
        NotificationPreferenceRules.Allows(type, false, true, true, true, true).ShouldBeFalse();

    [Fact]
    public void A_Kind_That_Is_Off_Stays_Off_And_Leaves_The_Others_Alone()
    {
        // Salary off, the rest on.
        NotificationPreferenceRules.Allows(ContributionNotificationType.SalaryHelpful, true, true, false, true, true).ShouldBeFalse();
        NotificationPreferenceRules.Allows(ContributionNotificationType.ReviewHelpful, true, true, false, true, true).ShouldBeTrue();
        NotificationPreferenceRules.Allows(ContributionNotificationType.ExperienceHelpful, true, true, false, true, true).ShouldBeTrue();
        NotificationPreferenceRules.Allows(ContributionNotificationType.BlogCommentHelpful, true, true, false, true, true).ShouldBeTrue();
    }

    [Fact]
    public void Feed_Paging_Interleaves_Both_Sources_By_Time()
    {
        var t = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var contributions = new[] { Item(t.AddMinutes(-1)), Item(t.AddMinutes(-3)), Item(t.AddMinutes(-5)) };
        var emails = new[] { Item(t.AddMinutes(-2), NotificationFeedKind.Email), Item(t.AddMinutes(-4), NotificationFeedKind.Email) };

        var first = NotificationFeedPaging.Page(contributions, emails, page: 1, pageSize: 2);
        first.Select(i => i.OccurredAt).ShouldBe([t.AddMinutes(-1), t.AddMinutes(-2)]);

        var second = NotificationFeedPaging.Page(contributions, emails, page: 2, pageSize: 2);
        second.Select(i => i.OccurredAt).ShouldBe([t.AddMinutes(-3), t.AddMinutes(-4)]);

        var third = NotificationFeedPaging.Page(contributions, emails, page: 3, pageSize: 2);
        third.Select(i => i.OccurredAt).ShouldBe([t.AddMinutes(-5)]);
    }

    [Fact]
    public void Feed_Paging_Breaks_Time_Ties_By_Id_So_Pages_Do_Not_Overlap()
    {
        var t = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var a = Item(t);
        var b = Item(t, NotificationFeedKind.Email);

        var page1 = NotificationFeedPaging.Page([a], [b], 1, 1).Single();
        var page2 = NotificationFeedPaging.Page([a], [b], 2, 1).Single();

        page1.Id.ShouldNotBe(page2.Id);
        new[] { page1.Id, page2.Id }.ShouldBe(new[] { a.Id, b.Id }.OrderByDescending(id => id));
    }

    [Theory]
    [InlineData(0, 10, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 51, false)]
    [InlineData(101, 10, false)]
    [InlineData(1, 10, true)]
    [InlineData(100, 50, true)]
    public void Feed_Query_Bounds_Page_And_Size(int page, int pageSize, bool valid) =>
        new GetNotificationFeedQueryValidator().Validate(new GetNotificationFeedQuery(page, pageSize)).IsValid.ShouldBe(valid);

    private static NotificationFeedItemResponse Item(DateTimeOffset at, NotificationFeedKind kind = NotificationFeedKind.Contribution) =>
        new(Guid.CreateVersion7(), kind, at, false,
            kind == NotificationFeedKind.Contribution
                ? new ContributionNotificationResponse(ContributionNotificationType.ReviewHelpful, Guid.NewGuid(), 1, "Acme", "acme", null, null, null)
                : null,
            (EmailNotificationResponse?)null);
}
