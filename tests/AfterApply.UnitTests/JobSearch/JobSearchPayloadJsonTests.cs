using AfterApply.Application.JobSearch.Contracts;
using AfterApply.Infrastructure.JobSearch;
using Shouldly;

namespace AfterApply.UnitTests.JobSearch;

/// <summary>What goes into the jsonb columns comes back as the same DTO — and something that is
/// not that DTO comes back as null, never as an exception the user sees.</summary>
public class JobSearchPayloadJsonTests
{
    [Fact]
    public async Task A_Search_Response_Round_Trips()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(System.Net.HttpStatusCode.OK, JSearchFixtures.Read("search-v2.json"));
        var wire = await client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None);
        var response = new JobSearchResultsResponse(wire.Data.Jobs!.Select(JobSearchMapper.ToSummary).ToList(), wire.Data.Cursor,
            new JobSearchMeta(false, new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero), 1));

        var json = JobSearchPayloadJson.Serialize(response);
        var back = JobSearchPayloadJson.Deserialize<JobSearchResultsResponse>(json);

        back.ShouldNotBeNull();
        back.Jobs.Count.ShouldBe(2);
        back.Jobs[1].Salary!.Min.ShouldBe(57500);
        back.Jobs[1].Benefits.ShouldBe(["dental_coverage", "health_insurance"]);
        back.NextCursor.ShouldBe("cursor-page-2");
        back.Meta.ShouldBe(response.Meta);
    }

    [Fact]
    public void Enums_Are_Stored_By_Name()
    {
        var json = JobSearchPayloadJson.Serialize(new JobSearchUserOverridesResponse(null, null, null, JobSearchDatePosted.ThreeDays,
            null, null, null, null, DateTimeOffset.UnixEpoch));

        json.ShouldContain("\"ThreeDays\"");
    }

    [Fact]
    public void Json_Of_Another_Shape_Is_A_Miss()
    {
        JobSearchPayloadJson.Deserialize<JobSearchResultsResponse>("[1,2,3]").ShouldBeNull();
        JobSearchPayloadJson.Deserialize<JobSearchResultsResponse>("not json").ShouldBeNull();
    }

    [Fact]
    public void The_Schema_Version_Is_Positive()
    {
        JobSearchPayloadJson.SchemaVersion.ShouldBeGreaterThan(0);
    }
}
