using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Api.Middleware;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Auditing;
using AfterApply.Application.Benchmark.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.SiteTraffic.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Auditing;
using AfterApply.Domain.Benchmark;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Auditing;

/// <summary>
/// The request audit, end to end: every write under /api leaves a row with the caller's IP, the
/// few deliberate exceptions leave none, the rows go with the account, and nothing ever hands one
/// back to a caller. See DECISIONS.md 2026-09-14.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class RequestAuditTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // TEST-NET-3, never routable. Under the in-memory test server there is no TCP connection and
    // Connection.RemoteIpAddress is null, so the client says who it is the way Cloud Run's frontend
    // does in production: X-Forwarded-For, which Program.cs's forwarded-headers setup honours.
    private const string ClientIp = "203.0.113.7";

    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(RequestAuditTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await TestHostDisposal.DisposeQuietlyAsync(_factory);
        }
    }

    private HttpClient AnonymousClient()
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ClientIp);
        return client;
    }

    private async Task<(HttpClient Client, Guid UserId, AuthResponse Auth)> RegisterAsync(string email)
    {
        var client = AnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Audit", "Test", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth.User.Id, auth);
    }

    private static Task<HttpResponseMessage> CreateApplicationAsync(HttpClient client, string companyName) =>
        client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Engineer", null, null, EmploymentType.FullTime,
            DateTimeOffset.UtcNow.AddDays(-1), null, null), JsonOptions);

    private async Task<List<RequestAudit>> RowsForPathAsync(string path)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.RequestAudits.AsNoTracking().Where(a => a.Path == path).OrderBy(a => a.At).ToListAsync();
    }

    // The row is written after the handler, inside the same pipeline pass; the client's read of
    // the body completes when that pass does, so by the time a test looks the row is there. The
    // short poll is insurance against the test server ever completing the body early.
    private async Task<RequestAudit> SingleRowForPathAsync(string path)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var rows = await RowsForPathAsync(path);
            if (rows.Count > 0)
            {
                return rows.ShouldHaveSingleItem();
            }

            await Task.Delay(100);
        }

        throw new ShouldAssertException($"No request-audit row for {path}");
    }

    [Fact]
    public async Task A_Signed_In_Write_Leaves_A_Row_With_User_Ip_Path_And_Status()
    {
        var (client, userId, _) = await RegisterAsync("write.audit@example.com");

        var response = await CreateApplicationAsync(client, "Audit Co");
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var row = await SingleRowForPathAsync("/api/applications");
        row.UserId.ShouldBe(userId);
        row.Method.ShouldBe("POST");
        row.StatusCode.ShouldBe(201);
        row.IpAddress.ShouldBe(ClientIp);
        row.At.ShouldBeInRange(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task A_Read_Leaves_No_Row()
    {
        var (client, _, _) = await RegisterAsync("read.audit@example.com");

        (await client.GetAsync("/api/applications?page=1&pageSize=5")).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/users/me")).EnsureSuccessStatusCode();

        (await RowsForPathAsync("/api/applications")).ShouldBeEmpty();
        (await RowsForPathAsync("/api/users/me")).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Rejected_Write_Is_Recorded_With_Its_Status_And_Without_The_Query_String()
    {
        var (client, userId, _) = await RegisterAsync("rejected.audit@example.com");

        var response = await client.PostAsJsonAsync("/api/applications?source=test", new CreateApplicationRequest(
            "", "", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow, null, null), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var row = await SingleRowForPathAsync("/api/applications");
        row.UserId.ShouldBe(userId);
        row.StatusCode.ShouldBe(400);
        row.Path.ShouldNotContain("?");
    }

    [Fact]
    public async Task Anonymous_Input_Is_Recorded_Without_A_User()
    {
        var client = AnonymousClient();

        // Register is the first thing this class does everywhere else; here the user is looked for
        // by the row itself, so the table is inspected before any account exists for this path.
        var benchmark = await client.PostAsJsonAsync("/api/benchmark/submissions",
            new SubmitBenchmarkRequest(40, 8, BenchmarkSector.SoftwareAndIt, BenchmarkPeriod.LastSixMonths,
                null, null, "tr", null), JsonOptions);
        benchmark.StatusCode.ShouldBe(HttpStatusCode.OK);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("nobody.audit@example.com", "WrongPassword1!"), JsonOptions);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var benchmarkRow = await SingleRowForPathAsync("/api/benchmark/submissions");
        benchmarkRow.UserId.ShouldBeNull();
        benchmarkRow.StatusCode.ShouldBe(200);
        benchmarkRow.IpAddress.ShouldBe(ClientIp);

        var loginRow = await SingleRowForPathAsync("/api/auth/login");
        loginRow.UserId.ShouldBeNull();
        loginRow.StatusCode.ShouldBe(401);
        loginRow.IpAddress.ShouldBe(ClientIp);
    }

    [Fact]
    public async Task The_Opted_Out_Endpoints_Leave_No_Row()
    {
        var (client, _, auth) = await RegisterAsync("optout.audit@example.com");
        var anonymous = AnonymousClient();

        (await anonymous.PostAsJsonAsync("/api/site-traffic/events",
            new RecordSiteTrafficEventRequest("page_view", "/tr", null), JsonOptions)).EnsureSuccessStatusCode();

        var started = await anonymous.PostAsJsonAsync("/api/extension-pairing/requests",
            new StartExtensionPairingRequest("tr"), JsonOptions);
        started.EnsureSuccessStatusCode();
        var pairing = await started.Content.ReadFromJsonAsync<StartedExtensionPairingResponse>(JsonOptions);
        (await anonymous.PostAsJsonAsync("/api/extension-pairing/poll",
            new PollExtensionPairingRequest(pairing!.DeviceSecret), JsonOptions)).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken), JsonOptions)).EnsureSuccessStatusCode();

        // Register above did leave a row — that is the control showing the pipeline is on.
        (await RowsForPathAsync("/api/auth/register")).ShouldNotBeEmpty();
        (await RowsForPathAsync("/api/site-traffic/events")).ShouldBeEmpty();
        (await RowsForPathAsync("/api/extension-pairing/requests")).ShouldBeEmpty();
        (await RowsForPathAsync("/api/extension-pairing/poll")).ShouldBeEmpty();
        (await RowsForPathAsync("/api/auth/refresh")).ShouldBeEmpty();
    }

    // Opting out is a privacy statement, so the set of routes that carry the marker is pinned here.
    // A new WithoutRequestAudit() call fails this test until it is added below on purpose — with
    // its reason in the endpoint file and a line in DECISIONS.md.
    [Fact]
    public void Only_The_Agreed_Endpoints_Opt_Out_Of_The_Audit()
    {
        var writeVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

        var optedOut = _factory!.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is { } text && text.StartsWith("/api", StringComparison.Ordinal))
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Any(writeVerbs.Contains) == true)
            .Where(e => e.Metadata.GetMetadata<SkipRequestAuditMetadata>() is not null)
            .Select(e => e.RoutePattern.RawText!)
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

        optedOut.ShouldBe(
        [
            "/api/auth/refresh",
            "/api/extension-pairing/poll",
            "/api/extension-pairing/requests",
            "/api/site-traffic/events"
        ]);
    }

    [Fact]
    public async Task Deleting_The_Account_Takes_Its_Rows_And_Leaves_Anonymous_Ones()
    {
        var (client, userId, _) = await RegisterAsync("delete.audit@example.com");
        (await CreateApplicationAsync(client, "Delete Co")).EnsureSuccessStatusCode();
        await SingleRowForPathAsync("/api/applications");

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RequestAudits.Add(RequestAudit.Create(null, "POST", "/api/cv-scan", 200, ClientIp, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("P@ssw0rd123!"), options: JsonOptions)
        };
        (await client.SendAsync(delete)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await checkDb.RequestAudits.AnyAsync(a => a.UserId == userId)).ShouldBeFalse();
        (await checkDb.RequestAudits.AnyAsync(a => a.Path == "/api/cv-scan" && a.UserId == null)).ShouldBeTrue();
    }

    [Fact]
    public async Task The_Account_Export_Never_Contains_The_Audit()
    {
        var (client, _, _) = await RegisterAsync("export.audit@example.com");
        (await CreateApplicationAsync(client, "Export Co")).EnsureSuccessStatusCode();

        var export = await client.GetStringAsync("/api/users/me/export");

        export.ShouldNotContain(ClientIp);
        export.ShouldNotContain("ipAddress", Case.Insensitive);
        export.ShouldNotContain("requestAudit", Case.Insensitive);
    }

    [Fact]
    public async Task The_Purge_Removes_Old_Anonymous_Rows_Only()
    {
        var (_, userId, _) = await RegisterAsync("purge.audit@example.com");
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RequestAudits.AddRange(
                RequestAudit.Create(null, "POST", "/purge/old-anonymous", 200, ClientIp, now.AddDays(-400)),
                RequestAudit.Create(userId, "POST", "/purge/old-owned", 200, ClientIp, now.AddDays(-400)),
                RequestAudit.Create(null, "POST", "/purge/recent-anonymous", 200, ClientIp, now.AddDays(-10)));
            await db.SaveChangesAsync();
        }

        int deleted;
        using (var scope = _factory.Services.CreateScope())
        {
            deleted = await scope.ServiceProvider.GetRequiredService<IRequestAuditRetentionService>()
                .PurgeAnonymousAsync(CancellationToken.None);
        }

        deleted.ShouldBe(1);
        (await RowsForPathAsync("/purge/old-anonymous")).ShouldBeEmpty();
        (await RowsForPathAsync("/purge/old-owned")).ShouldHaveSingleItem();
        (await RowsForPathAsync("/purge/recent-anonymous")).ShouldHaveSingleItem();
    }
}
