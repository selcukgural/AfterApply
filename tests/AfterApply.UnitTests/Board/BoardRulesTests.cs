using AfterApply.Domain.Applications;
using AfterApply.Domain.Board;
using AfterApply.Domain.Common;
using Shouldly;

namespace AfterApply.UnitTests.Board;

public class BoardPositionsTests
{
    [Fact]
    public void An_Empty_Column_Starts_At_Zero()
    {
        BoardPositions.Top(null).ShouldBe(0);
        BoardPositions.Between(null, null).ShouldBe(0);
    }

    [Fact]
    public void Top_Goes_One_Gap_Above_The_Current_Top()
    {
        BoardPositions.Top(5 * BoardPositions.Gap).ShouldBe(4 * BoardPositions.Gap);
        BoardPositions.Top(0).ShouldBe(-BoardPositions.Gap);
    }

    [Fact]
    public void Between_Two_Cards_Is_The_Midpoint()
    {
        BoardPositions.Between(0, BoardPositions.Gap).ShouldBe(BoardPositions.Gap / 2);
    }

    [Fact]
    public void Between_With_One_Side_Open_Steps_One_Gap()
    {
        BoardPositions.Between(100, null).ShouldBe(100 + BoardPositions.Gap);
        BoardPositions.Between(null, 100).ShouldBe(100 - BoardPositions.Gap);
    }

    [Theory]
    [InlineData(10, 11)] // adjacent: no whole number strictly between
    [InlineData(10, 10)] // equal keys (two cards dropped on top at the same instant)
    [InlineData(11, 10)] // out of order
    public void No_Room_Asks_For_A_Renumber(long above, long below)
    {
        BoardPositions.Between(above, below).ShouldBeNull();
    }

    [Fact]
    public void A_Gap_Takes_About_Twenty_Inserts_Into_The_Same_Spot_Before_Renumbering()
    {
        // Keep inserting directly under the same card: the gap halves each time.
        long above = 0, below = BoardPositions.Gap;
        var inserts = 0;
        while (BoardPositions.Between(above, below) is { } key)
        {
            below = key;
            inserts++;
        }

        inserts.ShouldBe(20);
    }

    [Fact]
    public void Renumbered_Keys_Are_Evenly_Spaced_And_Ascending()
    {
        BoardPositions.Renumbered(3).ShouldBe([0, BoardPositions.Gap, 2 * BoardPositions.Gap]);
        BoardPositions.Renumbered(0).ShouldBeEmpty();
    }
}

public class BoardColumnsTests
{
    [Theory]
    [InlineData(ApplicationStatus.Applied, BoardColumn.Applied)]
    [InlineData(ApplicationStatus.Screening, BoardColumn.InProgress)]
    [InlineData(ApplicationStatus.Interview, BoardColumn.InProgress)]
    [InlineData(ApplicationStatus.TechnicalInterview, BoardColumn.InProgress)]
    [InlineData(ApplicationStatus.FinalInterview, BoardColumn.InProgress)]
    [InlineData(ApplicationStatus.Offer, BoardColumn.Offer)]
    [InlineData(ApplicationStatus.Accepted, BoardColumn.Closed)]
    [InlineData(ApplicationStatus.Rejected, BoardColumn.Closed)]
    [InlineData(ApplicationStatus.Withdrawn, BoardColumn.Closed)]
    [InlineData(ApplicationStatus.Ghosted, BoardColumn.Closed)]
    public void Every_Status_Has_Exactly_One_Column(ApplicationStatus status, BoardColumn column)
    {
        BoardColumns.For(status).ShouldBe(column);
        BoardColumns.StatusesIn(column).ShouldContain(status);
    }

    [Fact]
    public void The_Column_Sets_Partition_The_Statuses()
    {
        var all = Enum.GetValues<BoardColumn>().SelectMany(BoardColumns.StatusesIn).ToList();

        all.ShouldBe(Enum.GetValues<ApplicationStatus>(), ignoreOrder: true);
        BoardColumns.StatusesIn(BoardColumn.Saved).ShouldBeEmpty();
    }
}

public class BoardAutoPlacementTests
{
    private static readonly ApplicationStatus[] Progress =
    [
        ApplicationStatus.Screening, ApplicationStatus.Interview, ApplicationStatus.TechnicalInterview,
        ApplicationStatus.FinalInterview, ApplicationStatus.Offer
    ];

    public static TheoryData<ApplicationStatus> ProgressStatuses() => new(Progress);

    public static TheoryData<ApplicationStatus> NonProgressStatuses() =>
        new(Enum.GetValues<ApplicationStatus>().Except(Progress));

    [Theory]
    [MemberData(nameof(ProgressStatuses))]
    public void Email_Progress_Brings_A_Removed_Card_Back(ApplicationStatus status)
    {
        BoardAutoPlacement.ForStatusChange(onBoard: false, status, StatusChangeOrigin.EmailAutoApplied)
            .ShouldBe(BoardPlacementAction.ReturnFromEmail);
        BoardAutoPlacement.ForStatusChange(onBoard: false, status, StatusChangeOrigin.EmailSuggestionConfirmed)
            .ShouldBe(BoardPlacementAction.ReturnFromEmail);
    }

    [Theory]
    [MemberData(nameof(NonProgressStatuses))]
    public void A_Rejection_Or_Silence_Does_Not_Bring_A_Card_Back(ApplicationStatus status)
    {
        BoardAutoPlacement.ForStatusChange(onBoard: false, status, StatusChangeOrigin.EmailAutoApplied)
            .ShouldBe(BoardPlacementAction.None);
    }

    [Theory]
    [InlineData(StatusChangeOrigin.Manual)]
    [InlineData(StatusChangeOrigin.BulkEdit)]
    [InlineData(StatusChangeOrigin.Import)]
    [InlineData(StatusChangeOrigin.EmailAutoApplyReverted)]
    public void Nothing_But_Email_Puts_A_Card_Back(StatusChangeOrigin origin)
    {
        BoardAutoPlacement.ForStatusChange(onBoard: false, ApplicationStatus.Interview, origin)
            .ShouldBe(BoardPlacementAction.None);
    }

    [Fact]
    public void Email_Lifts_A_Card_On_The_Board_And_Marks_It_Even_For_A_Rejection()
    {
        BoardAutoPlacement.ForStatusChange(onBoard: true, ApplicationStatus.Rejected, StatusChangeOrigin.EmailAutoApplied)
            .ShouldBe(BoardPlacementAction.MoveToTopFromEmail);
    }

    [Theory]
    [InlineData(StatusChangeOrigin.Manual, BoardPlacementAction.MoveToTop)]
    [InlineData(StatusChangeOrigin.BulkEdit, BoardPlacementAction.MoveToTop)]
    [InlineData(StatusChangeOrigin.BulkEditReverted, BoardPlacementAction.None)]
    [InlineData(StatusChangeOrigin.EmailAutoApplyReverted, BoardPlacementAction.None)]
    [InlineData(StatusChangeOrigin.Import, BoardPlacementAction.None)]
    [InlineData(StatusChangeOrigin.System, BoardPlacementAction.None)]
    public void A_Card_On_The_Board_Moves_Only_For_The_Users_Own_Changes(StatusChangeOrigin origin, BoardPlacementAction expected)
    {
        BoardAutoPlacement.ForStatusChange(onBoard: true, ApplicationStatus.Interview, origin).ShouldBe(expected);
    }

    [Theory]
    [InlineData(Source.CsvImport, null)]
    [InlineData(Source.LinkedInImport, null)]
    [InlineData(Source.BrowserExtension, BoardCardOrigin.Extension)]
    [InlineData(Source.Email, BoardCardOrigin.Email)]
    [InlineData(Source.Manual, BoardCardOrigin.Manual)]
    [InlineData(Source.LinkedIn, BoardCardOrigin.Manual)]
    [InlineData(Source.KariyerNet, BoardCardOrigin.Manual)]
    public void New_Applications_Land_With_The_Origin_Of_How_They_Were_Made(Source source, BoardCardOrigin? expected)
    {
        BoardAutoPlacement.ForNewApplication(source).ShouldBe(expected);
    }
}

public class BoardCardTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(BoardCardOrigin.Seed, false)]
    [InlineData(BoardCardOrigin.Manual, false)]
    [InlineData(BoardCardOrigin.Email, true)]
    [InlineData(BoardCardOrigin.EmailReturned, true)]
    [InlineData(BoardCardOrigin.Extension, true)]
    [InlineData(BoardCardOrigin.Later, true)]
    public void Only_Cards_That_Arrived_Without_The_User_Are_Unseen(BoardCardOrigin origin, bool unseen)
    {
        var card = BoardCard.ForApplication(Guid.NewGuid(), Guid.NewGuid(), isClosed: false, 0, origin, Now);

        (card.SeenAt is null).ShouldBe(unseen);
    }

    [Fact]
    public void An_Email_Arrival_Shows_The_Mark_Again()
    {
        var card = BoardCard.ForApplication(Guid.NewGuid(), Guid.NewGuid(), isClosed: false, 0, BoardCardOrigin.Manual, Now);

        card.MarkArrived(BoardCardOrigin.Email);

        card.SeenAt.ShouldBeNull();
        card.Origin.ShouldBe(BoardCardOrigin.Email);
    }

    [Fact]
    public void Closing_Keeps_The_First_Close_Time_And_Reopening_Clears_It()
    {
        var card = BoardCard.ForApplication(Guid.NewGuid(), Guid.NewGuid(), isClosed: false, 0, BoardCardOrigin.Manual, Now);

        card.SyncClosed(true, Now);
        card.SyncClosed(true, Now.AddDays(3));
        card.ClosedAt.ShouldBe(Now);

        card.SyncClosed(false, Now.AddDays(4));
        card.ClosedAt.ShouldBeNull();
    }

    [Fact]
    public void A_Converted_Posting_Keeps_Its_Card()
    {
        var card = BoardCard.ForTrackedJob(Guid.NewGuid(), Guid.NewGuid(), 5, BoardCardOrigin.Later, Now);
        var applicationId = Guid.NewGuid();

        card.BecomeApplication(applicationId, -3);

        card.ApplicationId.ShouldBe(applicationId);
        card.TrackedJobId.ShouldBeNull();
        card.Position.ShouldBe(-3);
    }
}
