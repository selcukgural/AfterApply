using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AfterApply.Api.Imports;
using AfterApply.Application.Imports.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.Imports;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Imports;

/// <summary>
/// The import progress hub crosses instances (DECISIONS.md 2026-09-18 "PR C"). The uploader's
/// socket is on the instance that served the page; the import job runs on whichever instance's
/// worker fetched it. Here the socket is on the Variant and the send comes from the fixture's
/// IHubContext — two hosts, two SignalR servers, one Redis backplane between them. Before the
/// backplane the message never arrived.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SignalRBackplaneTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_Group_Send_On_One_Instance_Reaches_A_Socket_On_Another()
    {
        var socketInstance = host.Variant("socket-instance", _ => { });
        var (client, auth) = await host.RegisterAsync("backplane@example.com", on: socketInstance);

        // JoinBatch only admits the batch's owner, so there has to be a real batch row.
        var batchId = await host.WithDbAsync(async db =>
        {
            var batch = ImportBatch.Create(auth.User.Id, Source.CsvImport, "applications.csv", DateTimeOffset.UtcNow);
            db.ImportBatches.Add(batch);
            await db.SaveChangesAsync();
            return batch.Id;
        });

        await using var connection = Connect(socketInstance, () => MintTicketAsync(client));

        var received = new TaskCompletionSource<ImportSummaryResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<ImportSummaryResponse>("importStatusChanged", status => received.TrySetResult(status));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinBatch", batchId);

        // The push comes from the OTHER host's hub context — the shape of a job running elsewhere.
        var hubOnJobInstance = host.Services.GetRequiredService<IHubContext<ImportProgressHub>>();
        await hubOnJobInstance.Clients.Group(ImportProgressHub.GroupName(batchId))
            .SendAsync("importStatusChanged", new ImportSummaryResponse(
                batchId, Source.CsvImport, "applications.csv", ImportBatchStatus.Processing,
                ProcessedRows: 3, TotalRows: 10, TotalRecords: 0, NewApplications: 0, DuplicateRecords: 0, InvalidRecords: 0,
                CompletedAt: null, ErrorMessage: null, Errors: []));

        var status = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        status.Id.ShouldBe(batchId);
        status.ProcessedRows.ShouldBe(3);
    }

    /// <summary>The URL carries whatever the hub accepts, so the session token must not open it:
    /// only a ticket does.</summary>
    [Fact]
    public async Task The_Hub_Refuses_A_Session_Access_Token()
    {
        var (_, auth) = await host.RegisterAsync("hub-session-token@example.com");

        await using var connection = Connect(host, () => Task.FromResult<string?>(auth.AccessToken));

        var failure = await Should.ThrowAsync<HttpRequestException>(() => connection.StartAsync());
        failure.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Hub_Ticket_Opens_The_Hub_And_Nothing_Else()
    {
        var (client, _) = await host.RegisterAsync("hub-ticket@example.com");
        var ticket = await MintTicketAsync(client);

        await using var connection = Connect(host, () => Task.FromResult<string?>(ticket));
        await connection.StartAsync();
        connection.State.ShouldBe(HubConnectionState.Connected);

        var api = host.CreateClient();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ticket);
        (await api.GetAsync("/api/applications")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Minting_A_Ticket_Needs_A_Session()
    {
        var response = await host.CreateClient().PostAsync("/api/imports/progress-ticket", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>A fresh ticket per call, like the web client: tickets live a minute, and a
    /// long-polling connection presents one on every request.</summary>
    private static async Task<string?> MintTicketAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/imports/progress-ticket", null);
        response.EnsureSuccessStatusCode();
        response.Headers.CacheControl?.NoStore.ShouldBeTrue();
        return (await response.Content.ReadFromJsonAsync<HubTicketResponse>(ApiHost.JsonOptions))!.Ticket;
    }

    private static HubConnection Connect(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> instance,
        Func<Task<string?>> accessTokenProvider) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(instance.Server.BaseAddress, "/hubs/import-progress"), options =>
            {
                options.HttpMessageHandlerFactory = _ => instance.Server.CreateHandler();
                options.AccessTokenProvider = accessTokenProvider;
            })
            // The hub serialises enums as strings (Program.cs); the client has to read them back
            // the same way or the handler silently never fires.
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
}
