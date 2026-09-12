using System.Net;
using AfterApply.Infrastructure.JobSearch;
using Shouldly;

namespace AfterApply.UnitTests.JobSearch;

/// <summary>Both body shapes the provider documents — its own envelope and the gateway's — and
/// the rate-limit headers that ride on every one of them.</summary>
public class JSearchClientResponseParsingTests
{
    [Fact]
    public async Task An_OK_Envelope_Yields_Typed_Data_The_Request_Id_And_The_Rate_Limit()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, JSearchFixtures.Read("search-v2.json"),
            ("x-ratelimit-requests-limit", "200"), ("x-ratelimit-requests-remaining", "137"), ("x-ratelimit-requests-reset", "1234567"));

        var result = await client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None);

        result.RequestId.ShouldBe("bbd0b2bd-ae35-495e-853f-266255161667");
        result.RateLimit.ShouldBe(new JSearchRateLimit(200, 137, 1234567));
        result.Data.Cursor.ShouldBe("cursor-page-2");
        result.Data.Jobs!.Count.ShouldBe(2);
        result.Data.Jobs[1].JobMinSalary.ShouldBe(57500);
        result.Data.Jobs[1].JobBenefits.ShouldBe(["dental_coverage", "health_insurance"]);
        result.Data.Jobs[0].JobHighlights!.Value.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task Job_Details_Deserialises_The_Detail_Only_Members()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, JSearchFixtures.Read("job-details.json"));

        var result = await client.GetJobDetailsAsync(new JSearchJobDetailsRequest(["GrjRvpGsrjwtgFBuAAAAAA=="]), CancellationToken.None);

        var job = result.Data.ShouldHaveSingleItem();
        job.WorkArrangement.ShouldBe("remote");
        job.RequiredExperienceYears.ShouldBe(3);
        job.RequiredTechnologies!.Count.ShouldBe(6);
        job.HasManagementResponsibilities.ShouldBe(true);
        job.EmployerReviews!.Value.GetArrayLength().ShouldBe(2);
        job.EducationRequired.ShouldNotBeNull();
    }

    [Fact]
    public async Task Numbers_Sent_As_Strings_Are_Tolerated()
    {
        var (client, handler) = JSearchTestClient.Create();
        var body = JSearchFixtures.Read("estimated-salary.json").Replace("\"salary_count\": 60", "\"salary_count\": \"60\"");
        handler.Enqueue(HttpStatusCode.OK, body);

        var result = await client.GetEstimatedSalaryAsync(new JSearchEstimatedSalaryRequest("x", "y"), CancellationToken.None);

        result.Data.Single().SalaryCount.ShouldBe(60);
    }

    [Fact]
    public async Task An_ERROR_Envelope_With_A_400_Code_Is_A_Bad_Request()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.BadRequest, JSearchFixtures.Read("error-envelope.json"));

        var exception = await Should.ThrowAsync<JSearchException>(() =>
            client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None));

        exception.Failure.ShouldBe(JSearchFailure.BadRequest);
        exception.StatusCode.ShouldBe(400);
        exception.RequestId.ShouldBe("35dabdcd-b334-4600-afbc-d654b8af91cf");
        exception.Message.ShouldBe("Missing query");
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_Gateways_403_Is_Unauthorized()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.Forbidden, JSearchFixtures.Read("gateway-403.json"));

        var exception = await Should.ThrowAsync<JSearchException>(() =>
            client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None));

        exception.Failure.ShouldBe(JSearchFailure.Unauthorized);
        exception.StatusCode.ShouldBe(403);
        exception.Message.ShouldBe("You are not subscribed to this API.");
    }

    [Fact]
    public async Task The_Gateways_429_Is_Rate_Limited_And_Carries_The_Remaining_Count()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.TooManyRequests, JSearchFixtures.Read("gateway-429.json"), ("x-ratelimit-requests-remaining", "0"));

        var exception = await Should.ThrowAsync<JSearchException>(() =>
            client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None));

        exception.Failure.ShouldBe(JSearchFailure.RateLimited);
        exception.Data["remaining"].ShouldBe(0);
    }

    [Fact]
    public async Task A_Non_Json_Body_Is_Malformed_On_A_200_And_Upstream_On_A_5xx()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, "<html>maintenance</html>");
        handler.Enqueue(HttpStatusCode.BadGateway, "<html>502</html>");
        handler.Enqueue(HttpStatusCode.BadGateway, "<html>502</html>");

        var first = await Should.ThrowAsync<JSearchException>(() =>
            client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None));
        first.Failure.ShouldBe(JSearchFailure.Malformed);

        var second = await Should.ThrowAsync<JSearchException>(() =>
            client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None));
        second.Failure.ShouldBe(JSearchFailure.Upstream);
        second.StatusCode.ShouldBe(502);
    }

    [Fact]
    public async Task OK_Data_Of_The_Wrong_Shape_Is_Malformed_Not_A_Crash()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, """{"status":"OK","request_id":"r","data":"not an object"}""");

        var exception = await Should.ThrowAsync<JSearchException>(() =>
            client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None));

        exception.Failure.ShouldBe(JSearchFailure.Malformed);
        exception.RequestId.ShouldBe("r");
    }
}
