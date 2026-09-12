using System.Net;
using AfterApply.Application.JobSearch.Contracts;
using AfterApply.Infrastructure.JobSearch;
using Shouldly;

namespace AfterApply.UnitTests.JobSearch;

/// <summary>
/// The exact request the provider sees, parameter by parameter. Pinned because every mistake here
/// is either a 400 that still costs a round trip or, worse, a silently different search that
/// costs a credit and returns the wrong thing.
/// </summary>
public class JSearchClientUrlBuildingTests
{
    private static readonly string SearchBody = JSearchFixtures.Read("search-v2.json");

    [Fact]
    public async Task Search_With_Only_The_Required_Query_Sends_Nothing_Else()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, SearchBody);

        await client.SearchAsync(new JSearchSearchRequest("backend developer istanbul"), CancellationToken.None);

        var uri = handler.Requests.ShouldHaveSingleItem().RequestUri!;
        uri.AbsoluteUri.ShouldBe("https://jsearch.p.rapidapi.com/search-v2?query=backend%20developer%20istanbul");
    }

    [Fact]
    public async Task Search_Sends_Every_Optional_Parameter_In_The_Provider_Format()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, SearchBody);

        await client.SearchAsync(new JSearchSearchRequest(
            "software developers in berlin",
            Cursor: "abc==",
            NumPages: 3,
            Country: "de",
            Language: "de",
            Location: "Berlin, Germany",
            DatePosted: JobSearchDatePosted.ThreeDays,
            WorkFromHome: true,
            EmploymentTypes: [JobSearchEmploymentType.FullTime, JobSearchEmploymentType.Intern],
            JobRequirements: [JobSearchJobRequirement.NoDegree, JobSearchJobRequirement.Under3YearsExperience],
            Radius: 25,
            ExcludeJobPublishers: ["BeeBe", "Dice"],
            Fields: ["employer_name", "job_title"]), CancellationToken.None);

        var query = handler.Requests.Single().RequestUri!.Query;
        query.ShouldBe("?query=software%20developers%20in%20berlin&cursor=abc%3D%3D&num_pages=3&country=de&language=de" +
                       "&location=Berlin%2C%20Germany&date_posted=3days&work_from_home=true" +
                       "&employment_types=FULLTIME%2CINTERN&job_requirements=no_degree%2Cunder_3_years_experience" +
                       "&radius=25&exclude_job_publishers=BeeBe%2CDice&fields=employer_name%2Cjob_title");
    }

    [Fact]
    public async Task Search_Omits_The_Providers_Own_Defaults_Rather_Than_Sending_Them()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, SearchBody);

        await client.SearchAsync(new JSearchSearchRequest("x", DatePosted: JobSearchDatePosted.All, WorkFromHome: false,
            EmploymentTypes: [], JobRequirements: [], ExcludeJobPublishers: [], Language: "", Location: "   "), CancellationToken.None);

        var query = handler.Requests.Single().RequestUri!.Query;
        query.ShouldBe("?query=x");
    }

    [Fact]
    public async Task Search_Never_Sends_A_Language_The_Caller_Did_Not_Give()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, SearchBody);

        await client.SearchAsync(new JSearchSearchRequest("backend developer", Country: "tr"), CancellationToken.None);

        var query = handler.Requests.Single().RequestUri!.Query;
        query.ShouldBe("?query=backend%20developer&country=tr");
        query.ShouldNotContain("language");
    }

    [Fact]
    public async Task Turkish_Characters_Are_Percent_Encoded()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, SearchBody);

        await client.SearchAsync(new JSearchSearchRequest("yazılım geliştirici İstanbul"), CancellationToken.None);

        handler.Requests.Single().RequestUri!.Query
            .ShouldBe("?query=yaz%C4%B1l%C4%B1m%20geli%C5%9Ftirici%20%C4%B0stanbul");
    }

    [Fact]
    public async Task Job_Details_Joins_Ids_With_A_Comma_And_Keeps_Base64_Padding_Encoded()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, JSearchFixtures.Read("job-details.json"));

        await client.GetJobDetailsAsync(new JSearchJobDetailsRequest(["a==", "b", "c"], "tr", "en"), CancellationToken.None);

        var uri = handler.Requests.Single().RequestUri!;
        uri.AbsolutePath.ShouldBe("/job-details");
        uri.Query.ShouldBe("?job_id=a%3D%3D%2Cb%2Cc&country=tr&language=en");
    }

    [Fact]
    public async Task Estimated_Salary_Sends_Enums_Only_When_They_Are_Not_The_Default()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, JSearchFixtures.Read("estimated-salary.json"));
        handler.Enqueue(HttpStatusCode.OK, JSearchFixtures.Read("estimated-salary.json"));

        await client.GetEstimatedSalaryAsync(new JSearchEstimatedSalaryRequest("nodejs developer", "new york"), CancellationToken.None);
        await client.GetEstimatedSalaryAsync(new JSearchEstimatedSalaryRequest("nodejs developer", "new york",
            JobSearchLocationType.City, JobSearchExperienceRange.FourToSix, ["job_title", "median_salary"]), CancellationToken.None);

        handler.Requests[0].RequestUri!.PathAndQuery.ShouldBe("/estimated-salary?job_title=nodejs%20developer&location=new%20york");
        handler.Requests[1].RequestUri!.PathAndQuery.ShouldBe("/estimated-salary?job_title=nodejs%20developer&location=new%20york" +
                                                             "&location_type=CITY&years_of_experience=FOUR_TO_SIX&fields=job_title%2Cmedian_salary");
    }

    [Fact]
    public async Task Company_Salary_Sends_Company_Title_And_Optional_Location()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, JSearchFixtures.Read("company-job-salary.json"));

        await client.GetCompanyJobSalaryAsync(new JSearchCompanySalaryRequest("Amazon", "software developer", "Seattle",
            JobSearchLocationType.State, JobSearchExperienceRange.AboveFifteen), CancellationToken.None);

        handler.Requests.Single().RequestUri!.PathAndQuery.ShouldBe(
            "/company-job-salary?company=Amazon&job_title=software%20developer&location=Seattle&location_type=STATE&years_of_experience=ABOVE_FIFTEEN");
    }

    [Fact]
    public async Task The_RapidAPI_Headers_Travel_With_Each_Request_Not_On_The_Client()
    {
        var options = JSearchTestClient.Options(o => o.ApiKey = "secret-key");
        var (client, handler) = JSearchTestClient.Create(options);
        handler.Enqueue(HttpStatusCode.OK, SearchBody);

        await client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None);

        var request = handler.Requests.Single();
        request.Headers.GetValues("x-rapidapi-key").ShouldBe(["secret-key"]);
        request.Headers.GetValues("x-rapidapi-host").ShouldBe(["jsearch.p.rapidapi.com"]);
        request.Method.ShouldBe(HttpMethod.Get);
    }

    [Fact]
    public async Task Without_A_Key_Nothing_Is_Sent()
    {
        var (client, handler) = JSearchTestClient.Create(JSearchTestClient.Options(o => o.ApiKey = ""));

        var exception = await Should.ThrowAsync<JSearchException>(() =>
            client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None));

        exception.Failure.ShouldBe(JSearchFailure.NotConfigured);
        handler.Requests.ShouldBeEmpty();
    }
}
