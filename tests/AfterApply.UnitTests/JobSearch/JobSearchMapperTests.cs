using System.Net;
using System.Text.Json;
using AfterApply.Infrastructure.JobSearch;
using Shouldly;

namespace AfterApply.UnitTests.JobSearch;

/// <summary>Every provider field lands on the property it should — for all four shapes — and a
/// posting that is mostly null maps to nulls and empty lists, never to an exception.</summary>
public class JobSearchMapperTests
{
    [Fact]
    public async Task A_Search_Posting_Maps_All_34_Fields()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, JSearchFixtures.Read("search-v2.json"));
        var wire = await client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None);

        var summary = JobSearchMapper.ToSummary(wire.Data.Jobs![1]);

        summary.JobId.ShouldBe("_EB5pZKZItD56RZ3AAAAAA==");
        summary.Title.ShouldBe("Entry Level Software Developer (Chicago)");
        summary.EmployerName.ShouldBe("SkillStorm");
        summary.EmployerLogo.ShouldBe("https://logo.example/skillstorm.png");
        summary.EmployerWebsite.ShouldBe("https://skillstorm.com");
        summary.Publisher.ShouldBe("SkillStorm");
        summary.EmploymentType.ShouldBe("Full-time");
        summary.EmploymentTypes.ShouldBe(["FULLTIME"]);
        summary.ApplyLink.ShouldBe("https://careers.skillstorm.com/jobs/42330");
        summary.ApplyIsDirect.ShouldBe(true);
        summary.ApplyOptions.Count.ShouldBe(2);
        summary.ApplyOptions[1].Publisher.ShouldBe("ZipRecruiter");
        summary.ApplyOptions[1].IsDirect.ShouldBe(false);
        summary.Description.ShouldStartWith("SkillStorm is actively seeking");
        summary.IsRemote.ShouldBe(false);
        summary.PostedAtText.ShouldBe("3 days ago");
        summary.PostedAtTimestamp.ShouldBe(1777248000);
        summary.PostedAtUtc.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1777248000));
        summary.Location.ShouldBe("Chicago, IL");
        summary.City.ShouldBe("Chicago");
        summary.State.ShouldBe("Illinois");
        summary.Country.ShouldBe("US");
        summary.Latitude.ShouldBe(41.88325);
        summary.Longitude.ShouldBe(-87.6323879);
        summary.Benefits.ShouldBe(["dental_coverage", "health_insurance"]);
        summary.BenefitLabels.ShouldBe(["Dental insurance", "Health insurance"]);
        summary.GoogleLink.ShouldBe("https://www.google.com/search?q=jobs#docid=2");
        summary.Salary.ShouldNotBeNull();
        summary.Salary.Value.ShouldBeNull();
        summary.Salary.Text.ShouldBe("57.5K–72.5K a year");
        summary.Salary.Min.ShouldBe(57500);
        summary.Salary.Max.ShouldBe(72500);
        summary.Salary.Period.ShouldBe("YEAR");
        summary.OnetSoc.ShouldBe("15125200");
        summary.OnetJobZone.ShouldBe("4");
    }

    [Fact]
    public async Task A_Null_Heavy_Posting_Maps_To_Nulls_And_Empty_Lists()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, JSearchFixtures.Read("search-v2.json"));
        var wire = await client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None);

        var summary = JobSearchMapper.ToSummary(wire.Data.Jobs![0]);

        summary.EmployerWebsite.ShouldBeNull();
        summary.Benefits.ShouldBeEmpty();
        summary.BenefitLabels.ShouldBeEmpty();
        summary.Salary.ShouldBeNull();
        summary.OnetSoc.ShouldBeNull();

        // And the detail mapper on the same search-shaped posting: {} highlights, null reviews.
        var detail = JobSearchMapper.ToDetail(wire.Data.Jobs[0]);
        detail.Highlights.ShouldBeNull();
        detail.EmployerReviews.ShouldBeEmpty();
        detail.RequiredTechnologies.ShouldBeEmpty();
        detail.RequiredExperienceYears.ShouldBeNull();
        detail.EducationRequired.ShouldBeNull();
    }

    [Fact]
    public async Task A_Detail_Posting_Maps_All_49_Fields()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, JSearchFixtures.Read("job-details.json"));
        var wire = await client.GetJobDetailsAsync(new JSearchJobDetailsRequest(["x"]), CancellationToken.None);

        var detail = JobSearchMapper.ToDetail(wire.Data.Single());

        detail.JobId.ShouldBe("GrjRvpGsrjwtgFBuAAAAAA==");
        detail.Title.ShouldBe("Senior Front-End Developer");
        detail.ApplyOptions.Count.ShouldBe(3);
        detail.IsRemote.ShouldBeNull();
        detail.PostedAtUtc.ShouldBe(new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero));
        detail.Benefits.ShouldBe(["dental_coverage", "health_insurance", "paid_time_off"]);
        detail.BenefitLabels.ShouldBeEmpty();
        detail.Salary!.Min.ShouldBe(91700);
        detail.Salary.Max.ShouldBe(138000);
        detail.Salary.Period.ShouldBe("YEAR");
        detail.Highlights.ShouldNotBeNull();
        detail.Highlights.Qualifications.ShouldBe(["3 or more years of experience", "Strong proficiency with TypeScript"]);
        detail.Highlights.Benefits.Count.ShouldBe(2);
        detail.Highlights.Responsibilities.Count.ShouldBe(2);
        detail.OnetSoc.ShouldBe("15113400");
        detail.OnetJobZone.ShouldBe("3");
        detail.EmployerReviews.Count.ShouldBe(2);
        detail.EmployerReviews[1].Publisher.ShouldBe("Glassdoor");
        detail.EmployerReviews[1].Score.ShouldBe(3.6);
        detail.EmployerReviews[1].NumStars.ShouldBe(3.5);
        detail.EmployerReviews[1].ReviewCount.ShouldBe(9907);
        detail.EmployerReviews[1].MaxScore.ShouldBe(5);
        detail.EmployerReviews[1].ReviewsLink.ShouldStartWith("https://www.glassdoor.com/");
        detail.WorkArrangement.ShouldBe("remote");
        detail.SeniorityLevel.ShouldBe("senior");
        detail.RequiredExperienceYears.ShouldBe(3);
        detail.EducationRequired.ShouldBe(new Application.JobSearch.Contracts.JobSearchEducationResponse(null, null));
        detail.VisaSponsorship.ShouldBeNull();
        detail.RelocationRequired.ShouldBeNull();
        detail.RelocationAssistance.ShouldBeNull();
        detail.ContractDuration.ShouldBeNull();
        detail.StartDate.ShouldBeNull();
        detail.RequiredTechnologies.ShouldBe(["HTML", "CSS", "JavaScript", "TypeScript", "React", "Next.js"]);
        detail.PreferredTechnologies.ShouldBeEmpty();
        detail.Methodologies.ShouldBe(["Agile", "Git workflows", "Code review"]);
        detail.Industry.ShouldBe("IT Services & Consulting");
        detail.JobFunction.ShouldBe("frontend");
        detail.HasManagementResponsibilities.ShouldBe(true);
        detail.AiMlInvolved.ShouldBe(true);
        detail.BenefitsExtended.Count.ShouldBe(4);
        detail.SoftSkills.ShouldBe(["Effective communicator", "Collaboration", "Mentorship"]);
    }

    [Fact]
    public async Task A_Salary_Estimate_Maps_All_18_Fields()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, JSearchFixtures.Read("estimated-salary.json"));
        var wire = await client.GetEstimatedSalaryAsync(new JSearchEstimatedSalaryRequest("x", "y"), CancellationToken.None);

        var estimate = JobSearchMapper.ToSalaryEstimate(wire.Data.Single());

        estimate.Location.ShouldBe("New York City, NY");
        estimate.JobTitle.ShouldBe("Nodejs Developer");
        estimate.MinSalary.ShouldBe(111524.6);
        estimate.MaxSalary.ShouldBe(185193.46);
        estimate.MedianSalary.ShouldBe(142629.56);
        estimate.MinBaseSalary.ShouldBe(81574.18);
        estimate.MaxBaseSalary.ShouldBe(129286.01);
        estimate.MedianBaseSalary.ShouldBe(102695.67);
        estimate.MinAdditionalPay.ShouldBe(29950.42);
        estimate.MaxAdditionalPay.ShouldBe(55907.45);
        estimate.MedianAdditionalPay.ShouldBe(39933.89);
        estimate.SalaryPeriod.ShouldBe("YEAR");
        estimate.SalaryCurrency.ShouldBe("USD");
        estimate.SalaryCount.ShouldBe(60);
        estimate.SalariesUpdatedAt.ShouldBe(new DateTimeOffset(2024, 6, 6, 23, 59, 59, TimeSpan.Zero));
        estimate.PublisherName.ShouldBe("Glassdoor");
        estimate.PublisherLink.ShouldStartWith("https://www.glassdoor.com/Salaries/");
        estimate.Confidence.ShouldBe("CONFIDENT");
    }

    [Fact]
    public async Task A_Company_Salary_Maps_All_16_Fields()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.OK, JSearchFixtures.Read("company-job-salary.json"));
        var wire = await client.GetCompanyJobSalaryAsync(new JSearchCompanySalaryRequest("Amazon", "x"), CancellationToken.None);

        var salary = JobSearchMapper.ToCompanySalary(wire.Data.Single());

        salary.Company.ShouldBe("Amazon");
        salary.Location.ShouldBe("United States");
        salary.JobTitle.ShouldBe("Software Developer");
        salary.MinSalary.ShouldBe(140363.95);
        salary.MaxSalary.ShouldBe(209046.05);
        salary.MedianSalary.ShouldBe(170007.45);
        salary.MinBaseSalary.ShouldBe(111261.07);
        salary.MaxBaseSalary.ShouldBe(154720.67);
        salary.MedianBaseSalary.ShouldBe(131203.61);
        salary.MinAdditionalPay.ShouldBe(29102.88);
        salary.MaxAdditionalPay.ShouldBe(54325.38);
        salary.MedianAdditionalPay.ShouldBe(38803.84);
        salary.SalaryPeriod.ShouldBe("YEAR");
        salary.SalaryCurrency.ShouldBe("USD");
        salary.SalaryCount.ShouldBe(974);
        salary.Confidence.ShouldBe("CONFIDENT");
    }

    [Fact]
    public void Posted_At_Falls_Back_To_The_Iso_String_When_The_Timestamp_Is_Missing()
    {
        var job = JsonSerializer.Deserialize<JSearchJob>(
            """{"job_id":"a","job_posted_at_timestamp":null,"job_posted_at_datetime_utc":"2026-04-28T00:00:00.000Z"}""",
            JSearchWireValues.JsonOptions)!;

        JobSearchMapper.ToSummary(job).PostedAtUtc.ShouldBe(new DateTimeOffset(2026, 4, 28, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Highlights_Sent_As_An_Array_Or_Reviews_Sent_As_An_Object_Are_Ignored_Not_Fatal()
    {
        var job = JsonSerializer.Deserialize<JSearchJob>(
            """{"job_id":"a","job_highlights":[],"employer_reviews":{"publisher":"x"}}""",
            JSearchWireValues.JsonOptions)!;

        var detail = JobSearchMapper.ToDetail(job);

        detail.Highlights.ShouldBeNull();
        detail.EmployerReviews.ShouldBeEmpty();
    }
}
