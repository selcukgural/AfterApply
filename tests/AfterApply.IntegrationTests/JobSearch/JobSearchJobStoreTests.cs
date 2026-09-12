using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.JobSearch.Contracts;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AfterApply.IntegrationTests.JobSearch;

/// <summary>
/// The postings table: a search leaves every posting on file, a details call spends credits
/// only on the ids nobody has, and the second person — any person — reads a posting someone
/// else already opened for free.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class JobSearchJobStoreTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private JobSearchTestHost _host = null!;
    private HttpClient _alice = null!;
    private HttpClient _bob = null!;

    public async Task InitializeAsync()
    {
        _host = await JobSearchTestHost.StartAsync(shared, nameof(JobSearchJobStoreTests));
        _alice = await _host.RegisterAsync("alice.store@example.com");
        _bob = await _host.RegisterAsync("bob.store@example.com");
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task A_Search_Stores_Every_Posting_As_A_Summary_Without_Detail()
    {
        (await _alice.GetAsync("/api/job-search/jobs?query=developer")).EnsureSuccessStatusCode();

        var rows = await _host.QueryAsync(db => db.JobSearchJobs.OrderBy(j => j.JobId).ToListAsync());
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(r => r.Country == "tr" && r.Detail == null && r.DetailFetchedAt == null);
        rows.Select(r => r.JobId).ShouldBe(["PjUC9oZVlnm8rP7DAAAAAA==", "_EB5pZKZItD56RZ3AAAAAA=="]);
        rows.Single(r => r.JobId == "_EB5pZKZItD56RZ3AAAAAA==").EmployerName.ShouldBe("SkillStorm");
        rows[0].Summary.ShouldContain("\"jobId\"");
    }

    [Fact]
    public async Task Real_Length_Provider_Ids_Are_Stored_And_Served()
    {
        // The provider's v5 ids measured 402 characters on the first live call; the documentation's
        // 24-character samples were what the first cut of the column was sized for, and every
        // posting silently failed to store. This pins the real size end to end.
        var longId = new string('A', 200) + "==" + new string('b', 200);
        var body = JSearchFixtures.Read("search-v2.json").Replace("PjUC9oZVlnm8rP7DAAAAAA==", longId);
        _host.Handler.SearchBody = body;

        (await _alice.GetAsync("/api/job-search/jobs?query=long")).EnsureSuccessStatusCode();
        var stored = await _host.QueryAsync(db => db.JobSearchJobs.AnyAsync(j => j.JobId == longId));
        stored.ShouldBeTrue();

        var details = await _bob.GetFromJsonAsync<JobSearchJobDetailsResponse>(
            $"/api/job-search/jobs/details?ids={Uri.EscapeDataString(longId)}", JobSearchTestHost.Json);
        details!.Jobs.ShouldHaveSingleItem().JobId.ShouldBe(longId);
        details.Meta.CreditsCharged.ShouldBe(1);
        _host.Handler.UrisFor("/job-details").ShouldHaveSingleItem().Query.ShouldContain(Uri.EscapeDataString(longId));
    }

    [Fact]
    public async Task Details_Spend_One_Credit_Per_Id_And_Fill_The_Rows()
    {
        var response = await _alice.GetFromJsonAsync<JobSearchJobDetailsResponse>(
            "/api/job-search/jobs/details?ids=x1,y2", JobSearchTestHost.Json);

        response!.Jobs.Select(j => j.JobId).ShouldBe(["x1", "y2"]);
        response.Jobs[0].Title.ShouldBe("Detail for x1");
        response.Jobs[0].WorkArrangement.ShouldBe("remote");
        response.Meta.FromCache.ShouldBeFalse();
        response.Meta.CreditsCharged.ShouldBe(2);
        _host.Handler.UrisFor("/job-details").ShouldHaveSingleItem().Query.ShouldBe("?job_id=x1%2Cy2&country=tr");

        var rows = await _host.QueryAsync(db => db.JobSearchJobs.Where(j => j.JobId == "x1" || j.JobId == "y2").ToListAsync());
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(r => r.Detail != null && r.DetailExpiresAt != null);
    }

    [Fact]
    public async Task Another_User_Reads_A_Stored_Posting_For_Free()
    {
        (await _alice.GetAsync("/api/job-search/jobs/details?ids=shared1")).EnsureSuccessStatusCode();
        var before = _host.Handler.CallCount;

        var bobs = await _bob.GetFromJsonAsync<JobSearchJobDetailsResponse>(
            "/api/job-search/jobs/details?ids=shared1", JobSearchTestHost.Json);

        bobs!.Jobs.ShouldHaveSingleItem().JobId.ShouldBe("shared1");
        bobs.Meta.FromCache.ShouldBeTrue();
        bobs.Meta.CreditsCharged.ShouldBe(0);
        _host.Handler.CallCount.ShouldBe(before);

        var ledger = await _host.QueryAsync(db => db.JobSearchUsages.OrderBy(u => u.RequestedAt).ToListAsync());
        ledger.Count.ShouldBe(2);
        ledger[0].Credits.ShouldBe(1);
        ledger[1].Credits.ShouldBe(0);
        ledger[1].CacheHit.ShouldBeTrue();
    }

    [Fact]
    public async Task A_Batch_Only_Goes_Upstream_For_The_Ids_Nobody_Has_And_Keeps_The_Requested_Order()
    {
        (await _alice.GetAsync("/api/job-search/jobs/details?ids=known1")).EnsureSuccessStatusCode();

        var response = await _bob.GetFromJsonAsync<JobSearchJobDetailsResponse>(
            "/api/job-search/jobs/details?ids=new2,known1,new3", JobSearchTestHost.Json);

        response!.Jobs.Select(j => j.JobId).ShouldBe(["new2", "known1", "new3"]);
        response.Meta.CreditsCharged.ShouldBe(2);
        response.Meta.FromCache.ShouldBeFalse();
        _host.Handler.UrisFor("/job-details")[^1].Query.ShouldBe("?job_id=new2%2Cnew3&country=tr");
    }

    [Fact]
    public async Task The_Same_Id_In_Another_Country_Is_Another_Row()
    {
        (await _alice.GetAsync("/api/job-search/jobs/details?ids=multi")).EnsureSuccessStatusCode();
        (await _alice.GetAsync("/api/job-search/jobs/details?ids=multi&country=de")).EnsureSuccessStatusCode();

        _host.Handler.UrisFor("/job-details").Count.ShouldBe(2);
        var rows = await _host.QueryAsync(db => db.JobSearchJobs.Where(j => j.JobId == "multi").Select(j => j.Country).ToListAsync());
        rows.OrderBy(c => c).ShouldBe(["de", "tr"]);
    }

    [Fact]
    public async Task An_Expired_Detail_Is_Fetched_Again()
    {
        (await _alice.GetAsync("/api/job-search/jobs/details?ids=stale")).EnsureSuccessStatusCode();
        await _host.QueryAsync(async db =>
        {
            await db.JobSearchJobs.Where(j => j.JobId == "stale")
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.DetailExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
            return 0;
        });

        var response = await _bob.GetFromJsonAsync<JobSearchJobDetailsResponse>(
            "/api/job-search/jobs/details?ids=stale", JobSearchTestHost.Json);

        response!.Meta.FromCache.ShouldBeFalse();
        response.Meta.CreditsCharged.ShouldBe(1);
        _host.Handler.UrisFor("/job-details").Count.ShouldBe(2);
    }

    [Fact]
    public async Task An_Id_The_Provider_No_Longer_Knows_Is_Omitted_But_Still_Charged()
    {
        _host.Handler.UnknownJobIds.Add("gone");

        var response = await _alice.GetFromJsonAsync<JobSearchJobDetailsResponse>(
            "/api/job-search/jobs/details?ids=here,gone", JobSearchTestHost.Json);

        response!.Jobs.Select(j => j.JobId).ShouldBe(["here"]);
        response.Meta.CreditsCharged.ShouldBe(2);
        (await _host.QueryAsync(db => db.JobSearchJobs.AnyAsync(j => j.JobId == "gone"))).ShouldBeFalse();
    }

    [Fact]
    public async Task Details_For_A_Posting_Seen_In_A_Search_Upgrade_Its_Row_Rather_Than_Duplicating_It()
    {
        (await _alice.GetAsync("/api/job-search/jobs?query=developer")).EnsureSuccessStatusCode();
        (await _alice.GetAsync("/api/job-search/jobs/details?ids=_EB5pZKZItD56RZ3AAAAAA%3D%3D")).EnsureSuccessStatusCode();

        var rows = await _host.QueryAsync(db => db.JobSearchJobs.Where(j => j.JobId == "_EB5pZKZItD56RZ3AAAAAA==").ToListAsync());
        var row = rows.ShouldHaveSingleItem();
        row.Detail.ShouldNotBeNull();
        row.Summary.ShouldContain("\"jobId\"");
    }
}
