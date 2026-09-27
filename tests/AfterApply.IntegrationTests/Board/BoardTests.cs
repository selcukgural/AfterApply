using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Board;
using AfterApply.Application.Board.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.TrackedJobs.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Board;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Board;

/// <summary>The board switched on, with a clock the tests move (seed window, closed-card expiry).
/// Starts at the real "now": applications are stamped with the wall clock, the board with this one.</summary>
public sealed class BoardProfile : IHostProfile
{
    public MutableTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Board:Enabled"] = "true"
        }));
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Clock));
    }

    public void Reset() => Clock.Reset();
}

/// <summary>
/// The applications board (DECISIONS.md 2026-09-27): the first-open seed, per-column keyset paging,
/// the user's own add / remove / move / reorder, and the automatic arrivals from e-mail and the
/// extension — each read back through the API, which is what the web app sees.
/// </summary>
public class BoardTests(ApiHost<BoardProfile> host) : IClassFixture<ApiHost<BoardProfile>>, IAsyncLifetime
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = ApiHost.JsonOptions;

    private HttpClient _client = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        (_client, var auth) = await host.RegisterAsync("board.owner@example.com");
        _userId = auth.User.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> CreateApplicationAsync(string company, string title = "Backend Engineer", HttpClient? client = null)
    {
        var response = await (client ?? _client).PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            company, title, null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-1), null, null), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(Json))!.Id;
    }

    private async Task<Guid> SavePostingAsync(string company, string title = "Platform Engineer")
    {
        var response = await _client.PostAsJsonAsync("/api/tracked-jobs",
            new CreateTrackedJobRequest(company, title, null, null, null), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TrackedJobResponse>(Json))!.Id;
    }

    private async Task ChangeStatusAsync(Guid applicationId, ApplicationStatus status)
    {
        var response = await _client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(status, null, null), Json);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>What an e-mail signal does once matched: the service's internal overload with an
    /// e-mail origin — the same call EmailForwardingService makes.</summary>
    private Task ChangeStatusFromEmailAsync(Guid applicationId, ApplicationStatus status) =>
        host.WithScopeAsync(services => services.GetRequiredService<IApplicationService>().ChangeStatusAsync(
            _userId, applicationId, status, DateTimeOffset.UtcNow,
            new StatusChangeContext(Source.Email, StatusChangeOrigin.EmailAutoApplied), CancellationToken.None));

    private async Task<BoardResponse> GetBoardAsync(string query = "", HttpClient? client = null)
    {
        var response = await (client ?? _client).GetAsync($"/api/board{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BoardResponse>(Json))!;
    }

    private async Task<BoardColumnPage> GetColumnAsync(BoardColumn column, string? cursor, int limit = 10)
    {
        var query = $"/api/board/columns/{column}?limit={limit}" + (cursor is null ? "" : $"&cursor={cursor}");
        var response = await _client.GetAsync(query);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BoardColumnPage>(Json))!;
    }

    private static BoardColumnPage Column(BoardResponse board, BoardColumn column) => board.Columns.Single(c => c.Column == column);

    private static BoardCardResponse CardFor(BoardResponse board, Guid itemId) =>
        board.Columns.SelectMany(c => c.Cards).Single(c => c.ItemId == itemId);

    private static BoardColumn? ColumnOf(BoardResponse board, Guid itemId) =>
        board.Columns.FirstOrDefault(c => c.Cards.Any(card => card.ItemId == itemId))?.Column;

    private async Task MoveAsync(Guid cardId, ApplicationStatus? toStatus = null, Guid? above = null, Guid? below = null,
        HttpStatusCode expected = HttpStatusCode.NoContent, HttpClient? client = null)
    {
        var response = await (client ?? _client).PostAsJsonAsync($"/api/board/cards/{cardId}/move",
            new MoveBoardCardRequest(toStatus, above, below), Json);
        response.StatusCode.ShouldBe(expected);
    }

    private Task AgeApplicationAsync(Guid applicationId, int days) => host.WithDbAsync(db => db.Applications
        .Where(a => a.Id == applicationId)
        .ExecuteUpdateAsync(s => s.SetProperty(a => a.UpdatedAt, DateTimeOffset.UtcNow.AddDays(-days))));

    [Fact]
    public async Task The_Board_Does_Not_Exist_While_The_Flag_Is_Off()
    {
        // Through the same configuration layer the profile used — a later in-memory source wins,
        // where UseSetting sits underneath it.
        var off = host.Variant("off", builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Board:Enabled"] = "false" })));
        var (client, _) = await host.RegisterAsync("board.off@example.com", on: off);

        (await client.GetAsync("/api/board")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/api/board/cards", new AddBoardCardsRequest([Guid.NewGuid()], null), Json))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_First_Opening_Takes_Recent_Open_Applications_And_Saved_Postings()
    {
        var recent = await CreateApplicationAsync("Akbank");
        var old = await CreateApplicationAsync("Logo");
        var rejected = await CreateApplicationAsync("Papara");
        var interviewing = await CreateApplicationAsync("Midas");
        var posting = await SavePostingAsync("Insider");
        await ChangeStatusAsync(rejected, ApplicationStatus.Rejected);
        await ChangeStatusAsync(interviewing, ApplicationStatus.Interview);
        await AgeApplicationAsync(old, 45);

        var board = await GetBoardAsync();

        ColumnOf(board, recent).ShouldBe(BoardColumn.Applied);
        ColumnOf(board, interviewing).ShouldBe(BoardColumn.InProgress);
        ColumnOf(board, posting).ShouldBe(BoardColumn.Saved);
        ColumnOf(board, old).ShouldBeNull();
        ColumnOf(board, rejected).ShouldBeNull();
        CardFor(board, recent).Origin.ShouldBe(BoardCardOrigin.Seed);
        board.UnseenCount.ShouldBe(0);

        // The seed happens once: an application created before the board existed but touched after
        // the first opening does not appear by itself on a second read.
        await AgeApplicationAsync(old, 0);
        ColumnOf(await GetBoardAsync(), old).ShouldBeNull();
    }

    [Fact]
    public async Task Two_Tabs_Opening_The_Board_At_Once_Seed_It_Once()
    {
        for (var i = 0; i < 5; i++)
        {
            await CreateApplicationAsync($"Company {i}");
        }

        var boards = await Task.WhenAll(GetBoardAsync(), GetBoardAsync(), GetBoardAsync());

        boards.ShouldAllBe(b => Column(b, BoardColumn.Applied).Total == 5);
        (await host.WithDbAsync(db => db.BoardCards.CountAsync(c => c.UserId == _userId))).ShouldBe(5);
    }

    [Fact]
    public async Task Columns_Page_Ten_At_A_Time_Without_Repeating_A_Card_When_Others_Arrive_On_Top()
    {
        await GetBoardAsync(); // seed an empty board, so new applications arrive as cards
        for (var i = 0; i < 23; i++)
        {
            await CreateApplicationAsync($"Paged {i:D2}");
        }

        var board = await GetBoardAsync();
        var first = Column(board, BoardColumn.Applied);
        first.Total.ShouldBe(23);
        first.Cards.Count.ShouldBe(10);
        first.NextCursor.ShouldNotBeNull();

        // A new card lands on top between the user's scrolls; the pages that follow are unaffected.
        var newcomer = await CreateApplicationAsync("Newcomer");

        var second = await GetColumnAsync(BoardColumn.Applied, first.NextCursor);
        var third = await GetColumnAsync(BoardColumn.Applied, second.NextCursor);

        second.Cards.Count.ShouldBe(10);
        third.Cards.Count.ShouldBe(3);
        third.NextCursor.ShouldBeNull();
        var seen = first.Cards.Concat(second.Cards).Concat(third.Cards).Select(c => c.ItemId).ToList();
        seen.Distinct().Count().ShouldBe(23);
        seen.ShouldNotContain(newcomer);
        Column(await GetBoardAsync(), BoardColumn.Applied).Cards[0].ItemId.ShouldBe(newcomer);
    }

    [Fact]
    public async Task Removing_A_Card_Keeps_The_Application_And_Adding_It_Back_Puts_It_On_Top()
    {
        var first = await CreateApplicationAsync("Getir");
        var second = await CreateApplicationAsync("Trendyol");
        var board = await GetBoardAsync();
        var card = CardFor(board, first);

        (await _client.DeleteAsync($"/api/board/cards/{card.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        ColumnOf(await GetBoardAsync(), first).ShouldBeNull();
        (await _client.GetAsync($"/api/applications/{first}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var offBoard = await _client.GetFromJsonAsync<PagedResult<ApplicationSummaryResponse>>("/api/applications?onBoard=false", Json);
        offBoard!.Items.Select(i => i.Id).ShouldBe([first]);
        var all = await _client.GetFromJsonAsync<PagedResult<ApplicationSummaryResponse>>("/api/applications", Json);
        all!.Items.Single(i => i.Id == second).OnBoard.ShouldBeTrue();

        var added = await _client.PostAsJsonAsync("/api/board/cards", new AddBoardCardsRequest([first, first], null), Json);
        (await added.Content.ReadFromJsonAsync<AddBoardCardsResponse>(Json))!.Added.ShouldBe(1);

        var after = await GetBoardAsync();
        Column(after, BoardColumn.Applied).Cards[0].ItemId.ShouldBe(first);
        CardFor(after, first).Origin.ShouldBe(BoardCardOrigin.Manual);
    }

    [Fact]
    public async Task The_Users_Own_Order_Is_Kept_Between_Visits()
    {
        var a = await CreateApplicationAsync("A");
        var b = await CreateApplicationAsync("B");
        var c = await CreateApplicationAsync("C");
        var board = await GetBoardAsync();
        // Seeded most recent first: C, B, A.
        Column(board, BoardColumn.Applied).Cards.Select(x => x.ItemId).ShouldBe([c, b, a]);

        await MoveAsync(CardFor(board, a).Id, above: CardFor(board, c).Id);
        Column(await GetBoardAsync(), BoardColumn.Applied).Cards.Select(x => x.ItemId).ShouldBe([c, a, b]);

        await MoveAsync(CardFor(board, c).Id, below: CardFor(board, b).Id);
        Column(await GetBoardAsync(), BoardColumn.Applied).Cards.Select(x => x.ItemId).ShouldBe([a, c, b]);

        await MoveAsync(CardFor(board, b).Id);
        Column(await GetBoardAsync(), BoardColumn.Applied).Cards.Select(x => x.ItemId).ShouldBe([b, a, c]);
    }

    [Fact]
    public async Task Inserting_Into_The_Same_Gap_Over_And_Over_Renumbers_The_Column_And_Keeps_The_Order()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 25; i++)
        {
            ids.Add(await CreateApplicationAsync($"Gap {i:D2}"));
        }

        var board = await GetBoardAsync("?limit=50");
        var cards = Column(board, BoardColumn.Applied).Cards;
        var anchor = cards[0];

        // Each card goes directly under the anchor, above the previously moved one: the gap halves
        // every time and runs out after twenty.
        var moved = new List<Guid>();
        foreach (var card in cards.Skip(1).Take(22))
        {
            await MoveAsync(card.Id, above: anchor.Id);
            moved.Insert(0, card.ItemId);
        }

        var order = Column(await GetBoardAsync("?limit=50"), BoardColumn.Applied).Cards.Select(x => x.ItemId).ToList();
        order[0].ShouldBe(anchor.ItemId);
        order.Skip(1).Take(22).ShouldBe(moved);
        order.Count.ShouldBe(25);
    }

    [Fact]
    public async Task Moving_Across_Columns_Changes_The_Status_As_A_Manual_Change()
    {
        var id = await CreateApplicationAsync("Sahibinden");
        var card = CardFor(await GetBoardAsync(), id);

        await MoveAsync(card.Id, ApplicationStatus.TechnicalInterview);

        var board = await GetBoardAsync();
        ColumnOf(board, id).ShouldBe(BoardColumn.InProgress);
        CardFor(board, id).Status.ShouldBe(ApplicationStatus.TechnicalInterview);
        var history = await _client.GetFromJsonAsync<List<ApplicationStatusHistoryResponse>>(
            $"/api/applications/{id}/status-history", Json);
        history![0].ToStatus.ShouldBe(ApplicationStatus.TechnicalInterview);
        history[0].Origin.ShouldBe(StatusChangeOrigin.Manual);
    }

    [Fact]
    public async Task A_Saved_Posting_Dropped_Into_In_Progress_Becomes_An_Application_With_That_Status()
    {
        var posting = await SavePostingAsync("Craftgate");
        var card = CardFor(await GetBoardAsync(), posting);

        await MoveAsync(card.Id, ApplicationStatus.Screening);

        var board = await GetBoardAsync();
        var moved = board.Columns.SelectMany(c => c.Cards).Single(c => c.Id == card.Id);
        moved.Kind.ShouldBe(BoardCardKind.Application);
        moved.Status.ShouldBe(ApplicationStatus.Screening);
        ColumnOf(board, moved.ItemId).ShouldBe(BoardColumn.InProgress);
        (await host.WithDbAsync(db => db.TrackedJobs.AnyAsync(t => t.Id == posting))).ShouldBeFalse();
    }

    [Fact]
    public async Task Another_Users_Cards_And_Applications_Are_Out_Of_Reach()
    {
        var mine = await CreateApplicationAsync("Hepsiburada");
        var myCard = CardFor(await GetBoardAsync(), mine);
        var (stranger, _) = await host.RegisterAsync("board.stranger@example.com");
        await GetBoardAsync(client: stranger);

        await MoveAsync(myCard.Id, ApplicationStatus.Offer, expected: HttpStatusCode.NotFound, client: stranger);
        (await stranger.DeleteAsync($"/api/board/cards/{myCard.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var added = await stranger.PostAsJsonAsync("/api/board/cards", new AddBoardCardsRequest([mine], null), Json);
        (await added.Content.ReadFromJsonAsync<AddBoardCardsResponse>(Json))!.Added.ShouldBe(0);

        // A neighbour id from someone else's board is ignored, not used to read a position.
        var other = await CreateApplicationAsync("Stranger's", client: stranger);
        var theirs = CardFor(await GetBoardAsync(client: stranger), other);
        await MoveAsync(theirs.Id, above: myCard.Id, client: stranger);

        var board = await GetBoardAsync();
        CardFor(board, mine).Status.ShouldBe(ApplicationStatus.Applied);
        (await GetBoardAsync(client: stranger)).Columns.SelectMany(c => c.Cards).ShouldNotContain(c => c.ItemId == mine);
    }

    [Fact]
    public async Task An_Email_About_Progress_Brings_A_Removed_Card_Back_Marked_But_A_Rejection_Does_Not()
    {
        var progress = await CreateApplicationAsync("Colendi");
        var rejection = await CreateApplicationAsync("Martı");
        var board = await GetBoardAsync();
        await _client.DeleteAsync($"/api/board/cards/{CardFor(board, progress).Id}");
        await _client.DeleteAsync($"/api/board/cards/{CardFor(board, rejection).Id}");

        await ChangeStatusFromEmailAsync(progress, ApplicationStatus.Interview);
        await ChangeStatusFromEmailAsync(rejection, ApplicationStatus.Rejected);

        var after = await GetBoardAsync();
        var returned = CardFor(after, progress);
        ColumnOf(after, progress).ShouldBe(BoardColumn.InProgress);
        returned.Origin.ShouldBe(BoardCardOrigin.EmailReturned);
        returned.Unseen.ShouldBeTrue();
        ColumnOf(after, rejection).ShouldBeNull();
        after.UnseenCount.ShouldBe(1);
    }

    [Fact]
    public async Task An_Email_Moves_A_Card_On_The_Board_To_The_Top_Of_Its_New_Column_And_Marks_It()
    {
        var first = await CreateApplicationAsync("First");
        var second = await CreateApplicationAsync("Second");
        await ChangeStatusAsync(first, ApplicationStatus.Screening);
        var board = await GetBoardAsync();
        ColumnOf(board, second).ShouldBe(BoardColumn.Applied);

        await ChangeStatusFromEmailAsync(second, ApplicationStatus.Interview);

        var after = await GetBoardAsync();
        Column(after, BoardColumn.InProgress).Cards[0].ItemId.ShouldBe(second);
        CardFor(after, second).Origin.ShouldBe(BoardCardOrigin.Email);
        CardFor(after, second).Unseen.ShouldBeTrue();

        // Moving the card is looking at it; the mark goes.
        await MoveAsync(CardFor(after, second).Id);
        CardFor(await GetBoardAsync(), second).Unseen.ShouldBeFalse();
    }

    [Fact]
    public async Task Extension_Arrivals_Are_Marked_And_Applying_To_A_Saved_Posting_Keeps_Its_Card()
    {
        await GetBoardAsync();
        const string url = "https://www.linkedin.com/jobs/view/4449445627/";
        var saved = await _client.PostAsJsonAsync("/api/tracked-jobs/from-extension",
            new CreateFromExtensionRequest("Peak", "Platform Engineer", url, null, null, null), Json);
        saved.EnsureSuccessStatusCode();

        var board = await GetBoardAsync();
        var savedCard = Column(board, BoardColumn.Saved).Cards.Single();
        savedCard.Origin.ShouldBe(BoardCardOrigin.Later);
        savedCard.Unseen.ShouldBeTrue();

        var applied = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Peak", "Platform Engineer", url, null, null, null), Json);
        applied.EnsureSuccessStatusCode();
        var fresh = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Obilet", "Software Engineer II", "https://www.linkedin.com/jobs/view/4449445628/",
                null, null, null), Json);
        fresh.EnsureSuccessStatusCode();

        var after = await GetBoardAsync();
        Column(after, BoardColumn.Saved).Cards.ShouldBeEmpty();
        var converted = after.Columns.SelectMany(c => c.Cards).Single(c => c.Id == savedCard.Id);
        converted.Kind.ShouldBe(BoardCardKind.Application);
        converted.Origin.ShouldBe(BoardCardOrigin.Extension);
        Column(after, BoardColumn.Applied).Cards.Count.ShouldBe(2);
        Column(after, BoardColumn.Applied).Cards.ShouldAllBe(c => c.Origin == BoardCardOrigin.Extension && c.Unseen);

        // The card's source is the site the posting was on, not "browser extension": the filter
        // for LinkedIn has to find what was saved from LinkedIn.
        converted.Source.ShouldBe(Source.LinkedIn);
        var linkedIn = await GetBoardAsync("?sources=LinkedIn");
        Column(linkedIn, BoardColumn.Applied).Total.ShouldBe(2);
        Column(await GetBoardAsync("?sources=KariyerNet"), BoardColumn.Applied).Total.ShouldBe(0);

        await _client.PostAsJsonAsync("/api/board/cards/seen", new MarkBoardCardsSeenRequest(null, All: true), Json);
        (await GetBoardAsync()).UnseenCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_Closed_Application_Stays_For_Two_Weeks_Then_Leaves_The_Board_But_Not_The_List()
    {
        var id = await CreateApplicationAsync("iyzico");
        await GetBoardAsync();
        await ChangeStatusAsync(id, ApplicationStatus.Rejected);

        var board = await GetBoardAsync();
        ColumnOf(board, id).ShouldBe(BoardColumn.Closed);
        CardFor(board, id).LeavesBoardAt.ShouldNotBeNull();

        host.Profile.Clock.Advance(TimeSpan.FromDays(15));
        ColumnOf(await GetBoardAsync(), id).ShouldBeNull();

        await host.WithScopeAsync(services => services.GetRequiredService<IBoardMaintenanceService>().PurgeClosedAsync(CancellationToken.None));
        (await host.WithDbAsync(db => db.BoardCards.AnyAsync(c => c.ApplicationId == id))).ShouldBeFalse();
        (await _client.GetAsync($"/api/applications/{id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Filters_Narrow_The_View_Without_Touching_The_Board()
    {
        var quiet = await CreateApplicationAsync("Quiet Co");
        var busy = await CreateApplicationAsync("Busy Co", "Data Engineer");
        await GetBoardAsync();
        await AgeApplicationAsync(quiet, 20);

        var silent = await GetBoardAsync("?silentOnly=true");
        Column(silent, BoardColumn.Applied).Cards.Select(c => c.ItemId).ShouldBe([quiet]);

        var searched = await GetBoardAsync("?search=data");
        Column(searched, BoardColumn.Applied).Cards.Select(c => c.ItemId).ShouldBe([busy]);

        Column(await GetBoardAsync(), BoardColumn.Applied).Total.ShouldBe(2);
        (await _client.GetAsync("/api/board/columns/Applied?cursor=garbage")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_Board_Is_In_The_Data_Export()
    {
        var id = await CreateApplicationAsync("Export Co");
        await GetBoardAsync();

        var export = await _client.GetFromJsonAsync<AccountExportResponse>("/api/users/me/export", Json);

        export!.BoardCards.ShouldNotBeNull();
        export.BoardCards!.Single().ApplicationId.ShouldBe(id);
    }
}
