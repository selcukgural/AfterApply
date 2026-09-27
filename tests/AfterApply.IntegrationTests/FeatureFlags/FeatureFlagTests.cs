using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.ClientConfig;
using AfterApply.Application.FeatureFlags;
using AfterApply.Application.FeatureFlags.Contracts;
using AfterApply.Infrastructure.FeatureFlags;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;

namespace AfterApply.IntegrationTests.FeatureFlags;

/// <summary>The app as shipped (the board off by default), with a clock the tests move for the
/// confirmation token's lifetime.</summary>
public sealed class FeatureFlagProfile : IHostProfile
{
    public MutableTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public void Configure(IWebHostBuilder builder) =>
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Clock));

    public void Reset() => Clock.Reset();
}

/// <summary>
/// Runtime feature flags (DECISIONS.md 2026-09-27): only admins switch them, always in two
/// confirmed steps, every switch lands on every instance and in the history with who and from
/// where, and a flag nobody switched runs on its deploy default exactly as before.
/// </summary>
public class FeatureFlagTests(ApiHost<FeatureFlagProfile> host) : IClassFixture<ApiHost<FeatureFlagProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = ApiHost.JsonOptions;

    private HttpClient _admin = null!;
    private Guid _adminId;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        (_admin, var auth) = await host.RegisterAsync("flags.admin@example.com");
        _adminId = auth.User.Id;
        await host.MakeAdminAsync(_adminId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<PrepareFeatureFlagChangeResponse> PrepareAsync(HttpClient client, string flag, bool? enabled,
        string reason = "Launching after the help topic went live")
    {
        var response = await client.PostAsJsonAsync($"/api/admin/feature-flags/{flag}/prepare",
            new PrepareFeatureFlagChangeRequest(enabled, reason), Json);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PrepareFeatureFlagChangeResponse>(Json))!;
    }

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string flag, string token, string phrase) =>
        client.PostAsJsonAsync($"/api/admin/feature-flags/{flag}/confirm",
            new ConfirmFeatureFlagChangeRequest(token, phrase), Json);

    private async Task SwitchAsync(string flag, bool? enabled, HttpClient? client = null)
    {
        var prepared = await PrepareAsync(client ?? _admin, flag, enabled);
        var confirmed = await ConfirmAsync(client ?? _admin, flag, prepared.ConfirmationToken, prepared.ConfirmationPhrase);
        confirmed.StatusCode.ShouldBe(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<bool> BoardInConfigAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<ClientConfigResponse>("/api/config", Json))!.Board!.Enabled;

    [Fact]
    public async Task Only_Admins_Reach_The_Flags()
    {
        var (ordinary, _) = await host.RegisterAsync("flags.ordinary@example.com");
        var anonymous = host.CreateClient();

        (await anonymous.GetAsync("/api/admin/feature-flags")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ordinary.GetAsync("/api/admin/feature-flags")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ordinary.GetAsync("/api/admin/feature-flags/history")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ordinary.PostAsJsonAsync("/api/admin/feature-flags/Board/prepare",
            new PrepareFeatureFlagChangeRequest(true, "Trying my luck here"), Json)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A token an admin was issued is still no use to anyone else — the role is checked first.
        var prepared = await PrepareAsync(_admin, "Board", true);
        (await ConfirmAsync(ordinary, "Board", prepared.ConfirmationToken, "Board")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_Admins_Extension_Token_Reaches_None_Of_It()
    {
        // The extension's token sits in chrome.storage and in the Gmail content script's world; a
        // leak of it must not switch the product's features, even when its owner is an admin.
        var created = await _admin.PostAsJsonAsync("/api/personal-access-tokens",
            new AfterApply.Application.Identity.Contracts.CreatePersonalAccessTokenRequest("Chrome Extension"), Json);
        created.EnsureSuccessStatusCode();
        var token = (await created.Content.ReadFromJsonAsync<AfterApply.Application.Identity.Contracts.CreatedPersonalAccessTokenResponse>(Json))!.Token;
        using var extension = host.CreateClient();
        extension.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        (await extension.GetAsync("/api/admin/feature-flags")).StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        (await extension.GetAsync("/api/admin/feature-flags/history")).StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        (await extension.PostAsJsonAsync("/api/admin/feature-flags/Board/prepare",
            new PrepareFeatureFlagChangeRequest(true, "Through the extension token"), Json)).StatusCode
            .ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);

        var prepared = await PrepareAsync(_admin, "Board", true);
        (await ConfirmAsync(extension, "Board", prepared.ConfirmationToken, "Board")).StatusCode
            .ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        (await host.WithDbAsync(db => db.FeatureFlagOverrides.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task A_Token_Does_Not_Come_Back_To_Life_When_The_Flag_Returns_To_Where_It_Was()
    {
        var (other, otherAuth) = await host.RegisterAsync("flags.other@example.com");
        await host.MakeAdminAsync(otherAuth.User.Id);

        // Prepared while there is no override; used once; then another admin resets the flag, so the
        // override is gone again — the same state the token was issued in.
        var prepared = await PrepareAsync(_admin, "Board", true);
        (await ConfirmAsync(_admin, "Board", prepared.ConfirmationToken, "Board")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await SwitchAsync("Board", null, other);
        (await host.WithDbAsync(db => db.FeatureFlagOverrides.CountAsync())).ShouldBe(0);

        var replay = await ConfirmAsync(_admin, "Board", prepared.ConfirmationToken, "Board");
        replay.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(replay)).ShouldBe("FEATURE_FLAG_CHANGED_SINCE_PREPARE");
        (await host.WithDbAsync(db => db.FeatureFlagOverrides.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task The_First_Step_Says_How_Long_The_Token_Lives_Without_Relying_On_Clocks()
    {
        var prepared = await PrepareAsync(_admin, "Board", true);
        prepared.ExpiresInSeconds.ShouldBe(300);
    }

    [Fact]
    public async Task Every_Flag_Is_Listed_On_Its_Default_Until_Someone_Switches_It()
    {
        var flags = (await _admin.GetFromJsonAsync<List<FeatureFlagResponse>>("/api/admin/feature-flags", Json))!;

        flags.Select(f => f.Flag).ShouldBe(Enum.GetValues<FeatureFlag>(), ignoreOrder: true);
        flags.ShouldAllBe(f => f.Override == null && f.Enabled == f.Default && f.UpdatedAt == null);
        // appsettings.json: the board ships off, company reviews on.
        flags.Single(f => f.Flag == FeatureFlag.Board).Enabled.ShouldBeFalse();
        flags.Single(f => f.Flag == FeatureFlag.CompanyReviews).Enabled.ShouldBeTrue();
        flags.Single(f => f.Flag == FeatureFlag.Payments).Couplings.ShouldContain(FeatureFlagCoupling.Money);
        flags.Single(f => f.Flag == FeatureFlag.CvScanNotes).Couplings.ShouldContain(FeatureFlagCoupling.PrivacyText);
        flags.ShouldAllBe(f => f.Title.Length > 0 && f.Description.Length > 0 && f.WhenOff.Length > 0);
    }

    [Fact]
    public async Task The_Texts_Follow_The_Request_Language()
    {
        // Turkish is the API's default; English on request.
        var turkish = (await _admin.GetFromJsonAsync<List<FeatureFlagResponse>>("/api/admin/feature-flags", Json))!;
        turkish.Single(f => f.Flag == FeatureFlag.Board).Title.ShouldBe("Başvuru panosu (Pano)");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/feature-flags");
        request.Headers.AcceptLanguage.ParseAdd("en");
        var response = await _admin.SendAsync(request);
        var english = (await response.Content.ReadFromJsonAsync<List<FeatureFlagResponse>>(Json))!;
        english.Single(f => f.Flag == FeatureFlag.Board).Title.ShouldBe("Applications board");
        english.Single(f => f.Flag == FeatureFlag.Board).Notes.ShouldBeNull();
        english.Single(f => f.Flag == FeatureFlag.Payments).Notes.ShouldNotBeNull();
    }

    [Fact]
    public async Task Nothing_Changes_Until_The_Second_Step_Then_The_Feature_Is_On_Everywhere_It_Is_Read()
    {
        (await _admin.GetAsync("/api/board")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var prepared = await PrepareAsync(_admin, "Board", true);
        prepared.WillBeOn.ShouldBeTrue();
        prepared.ConfirmationPhrase.ShouldBe("Board");
        prepared.Current.Enabled.ShouldBeFalse();
        prepared.ExpiresAt.ShouldBe(host.Profile.Clock.GetUtcNow().AddMinutes(5));

        // The first step alone switched nothing.
        (await _admin.GetAsync("/api/board")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.WithDbAsync(db => db.FeatureFlagOverrides.CountAsync())).ShouldBe(0);

        var confirmed = await ConfirmAsync(_admin, "Board", prepared.ConfirmationToken, "Board");
        confirmed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var flag = (await confirmed.Content.ReadFromJsonAsync<FeatureFlagResponse>(Json))!;
        flag.Enabled.ShouldBeTrue();
        flag.Override.ShouldBe(true);
        flag.Default.ShouldBeFalse();
        flag.UpdatedBy.ShouldBe("flags.admin@example.com");

        (await _admin.GetAsync("/api/board")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BoardInConfigAsync(host.CreateClient())).ShouldBeTrue();
    }

    [Fact]
    public async Task The_Change_Is_Recorded_With_The_Admin_And_The_Connection_But_The_Ip_Never_Leaves()
    {
        // TEST-NET-3. The in-memory server has no TCP connection, so the client names itself the
        // way Cloud Run's frontend does (X-Forwarded-For) — see RequestAuditTests.
        const string clientIp = "203.0.113.42";
        _admin.DefaultRequestHeaders.Add("X-Forwarded-For", clientIp);
        await SwitchAsync("Board", true);

        var history = await _admin.GetAsync("/api/admin/feature-flags/history?flag=Board");
        var raw = await history.Content.ReadAsStringAsync();
        var changes = JsonSerializer.Deserialize<List<FeatureFlagChangeResponse>>(raw, Json)!;
        var change = changes.ShouldHaveSingleItem();
        change.Flag.ShouldBe(FeatureFlag.Board);
        change.Enabled.ShouldBe(true);
        change.WasOn.ShouldBeFalse();
        change.IsOn.ShouldBeTrue();
        change.Reason.ShouldBe("Launching after the help topic went live");
        change.ChangedBy.ShouldBe("flags.admin@example.com");

        var origin = await host.WithDbAsync(db => db.FeatureFlagChangeOrigins.SingleAsync());
        origin.ChangeId.ShouldBe(change.Id);
        origin.UserId.ShouldBe(_adminId);
        origin.IpAddress.ShouldBe(clientIp);
        raw.ShouldNotContain(clientIp);
        (await _admin.GetStringAsync("/api/admin/feature-flags")).ShouldNotContain(clientIp);
    }

    [Fact]
    public async Task The_Typed_Name_Must_Match_Exactly()
    {
        var prepared = await PrepareAsync(_admin, "Board", true);

        (await ConfirmAsync(_admin, "Board", prepared.ConfirmationToken, "board")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ConfirmAsync(_admin, "Board", prepared.ConfirmationToken, "Blog")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ConfirmAsync(_admin, "Board", prepared.ConfirmationToken, "")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.WithDbAsync(db => db.FeatureFlagOverrides.CountAsync())).ShouldBe(0);

        // A wrong guess does not burn the token.
        (await ConfirmAsync(_admin, "Board", prepared.ConfirmationToken, "Board")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_Token_Is_Good_For_One_Change_By_One_Admin_For_Five_Minutes()
    {
        var (other, otherAuth) = await host.RegisterAsync("flags.other@example.com");
        await host.MakeAdminAsync(otherAuth.User.Id);

        var prepared = await PrepareAsync(_admin, "Board", true);

        // Another admin, another flag, a forged token.
        var otherAdmin = await ConfirmAsync(other, "Board", prepared.ConfirmationToken, "Board");
        otherAdmin.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(otherAdmin)).ShouldBe("FEATURE_FLAG_CONFIRMATION_INVALID");
        (await ConfirmAsync(_admin, "Blog", prepared.ConfirmationToken, "Blog")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ConfirmAsync(_admin, "Board", prepared.ConfirmationToken[..^4] + "AAAA", "Board")).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);

        // Used once; the second use finds the flag already changed.
        (await ConfirmAsync(_admin, "Board", prepared.ConfirmationToken, "Board")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var replay = await ConfirmAsync(_admin, "Board", prepared.ConfirmationToken, "Board");
        replay.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(replay)).ShouldBe("FEATURE_FLAG_CHANGED_SINCE_PREPARE");

        // Past its lifetime.
        var late = await PrepareAsync(_admin, "Board", false);
        host.Profile.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        var expired = await ConfirmAsync(_admin, "Board", late.ConfirmationToken, "Board");
        expired.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(expired)).ShouldBe("FEATURE_FLAG_CONFIRMATION_INVALID");
        (await _admin.GetAsync("/api/board")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_Change_Made_Between_The_Two_Steps_Is_Not_Overwritten()
    {
        var (other, otherAuth) = await host.RegisterAsync("flags.other@example.com");
        await host.MakeAdminAsync(otherAuth.User.Id);

        var mine = await PrepareAsync(_admin, "Board", true);
        await SwitchAsync("Board", false, other);

        var stale = await ConfirmAsync(_admin, "Board", mine.ConfirmationToken, "Board");
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(stale)).ShouldBe("FEATURE_FLAG_CHANGED_SINCE_PREPARE");
        (await host.WithDbAsync(db => db.FeatureFlagOverrides.SingleAsync())).Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Switching_Gmail_Scanning_Off_Tells_The_Web_To_Hide_The_Suggestions()
    {
        var anonymous = host.CreateClient();
        (await anonymous.GetFromJsonAsync<ClientConfigResponse>("/api/config", Json))!.EmailSignals!.Enabled.ShouldBeTrue();

        await SwitchAsync("EmailSignals", false);

        (await anonymous.GetFromJsonAsync<ClientConfigResponse>("/api/config", Json))!.EmailSignals!.Enabled.ShouldBeFalse();
        (await _admin.GetAsync("/api/email-forwarding/suggestions/count")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Resetting_Returns_The_Flag_To_Its_Deploy_Default()
    {
        await SwitchAsync("CompanyReviews", false);
        (await host.CreateClient().GetFromJsonAsync<ClientConfigResponse>("/api/config", Json))!
            .CompanyReviews!.Enabled.ShouldBeFalse();

        host.Profile.Clock.Advance(TimeSpan.FromMinutes(1));
        var reset = await PrepareAsync(_admin, "CompanyReviews", null);
        reset.WillBeOn.ShouldBeTrue();
        (await ConfirmAsync(_admin, "CompanyReviews", reset.ConfirmationToken, "CompanyReviews")).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        (await host.WithDbAsync(db => db.FeatureFlagOverrides.CountAsync())).ShouldBe(0);
        (await host.CreateClient().GetFromJsonAsync<ClientConfigResponse>("/api/config", Json))!
            .CompanyReviews!.Enabled.ShouldBeTrue();
        var history = (await _admin.GetFromJsonAsync<List<FeatureFlagChangeResponse>>(
            "/api/admin/feature-flags/history?flag=CompanyReviews", Json))!;
        history.Select(c => (c.Enabled, c.WasOn, c.IsOn)).ShouldBe([(null, false, true), (false, true, false)]);
    }

    [Fact]
    public async Task Nothing_To_Change_And_Missing_Configuration_Are_Refused_At_The_First_Step()
    {
        var unchanged = await _admin.PostAsJsonAsync("/api/admin/feature-flags/Board/prepare",
            new PrepareFeatureFlagChangeRequest(null, "Reset without an override"), Json);
        unchanged.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(unchanged)).ShouldBe("FEATURE_FLAG_UNCHANGED");

        // The suite has no PayTR merchant: checkout cannot be switched on here, only off.
        var payments = await _admin.PostAsJsonAsync("/api/admin/feature-flags/Payments/prepare",
            new PrepareFeatureFlagChangeRequest(true, "Opening the Pro plan"), Json);
        payments.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(payments)).ShouldBe("FEATURE_FLAG_PREREQUISITE_MISSING");
        (await payments.Content.ReadAsStringAsync()).ShouldContain(FeatureFlagPrerequisites.PayTrConfiguration);
        await PrepareAsync(_admin, "Payments", false);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("why")]
    public async Task A_Reason_Is_Required(string? reason)
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/feature-flags/Board/prepare",
            new PrepareFeatureFlagChangeRequest(true, reason), Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("NoSuchFlag")]
    [InlineData("0")]
    [InlineData("99")]
    public async Task Only_Flag_Names_Name_A_Flag(string flag)
    {
        (await _admin.PostAsJsonAsync($"/api/admin/feature-flags/{flag}/prepare",
            new PrepareFeatureFlagChangeRequest(true, "Numbers are not names"), Json)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _admin.GetAsync($"/api/admin/feature-flags/history?flag={flag}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Names_Are_Read_Without_Regard_To_Case_In_The_Route()
    {
        var prepared = await PrepareAsync(_admin, "board", true);
        prepared.ConfirmationPhrase.ShouldBe("Board");
    }

    [Fact]
    public async Task Another_Instance_Picks_The_Switch_Up_Without_A_Restart()
    {
        var other = host.Variant("second-instance", _ => { });
        var otherClient = other.CreateClient();
        (await BoardInConfigAsync(otherClient)).ShouldBeFalse();

        var otherStore = other.Services.GetRequiredService<FeatureFlagStore>();
        // The subscription is made off the start-up path; publish only once the other side listens.
        await other.Services.GetRequiredService<FeatureFlagChannel>().Subscribed.WaitAsync(TimeSpan.FromSeconds(10));
        var reloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnReloaded()
        {
            if (otherStore.IsEnabled(FeatureFlag.Board))
            {
                reloaded.TrySetResult();
            }
        }

        otherStore.Reloaded += OnReloaded;
        try
        {
            await SwitchAsync("Board", true);
            // The Redis announcement, not the 15-second poll: well inside this bound.
            await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            otherStore.Reloaded -= OnReloaded;
        }

        (await BoardInConfigAsync(otherClient)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_Switch_Drops_Pages_Cached_Under_The_Old_Flags()
    {
        var cache = host.Services.GetRequiredService<IFusionCache>();
        await cache.SetAsync("flags-probe", "cached under the old flags");

        await SwitchAsync("CompanySalaries", false);

        (await cache.TryGetAsync<string>("flags-probe")).HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task Deleting_The_Admin_Keeps_The_Change_And_Drops_Who_And_Where()
    {
        await SwitchAsync("Board", true);

        await host.WithDbAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "Users" WHERE "Id" = {_adminId}"""));

        await host.WithDbAsync(async db =>
        {
            (await db.FeatureFlagChangeOrigins.CountAsync()).ShouldBe(0);
            var change = await db.FeatureFlagChanges.SingleAsync();
            change.ChangedByUserId.ShouldBeNull();
            change.Reason.ShouldBe("Launching after the help topic went live");
            var row = await db.FeatureFlagOverrides.SingleAsync();
            row.Enabled.ShouldBeTrue();
            row.UpdatedByUserId.ShouldBeNull();
        });
    }
}
