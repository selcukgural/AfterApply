using AfterApply.Application.JobSearch.Contracts;
using AfterApply.Application.JobSearch.Validators;
using Shouldly;

namespace AfterApply.UnitTests.JobSearch;

/// <summary>Anything the provider would 400 is refused before it costs a round trip.</summary>
public class JobSearchValidatorTests
{
    private readonly SearchJobsQueryValidator _search = new();
    private readonly GetJobDetailsQueryValidator _details = new();
    private readonly EstimatedSalaryQueryValidator _salary = new();
    private readonly CompanyJobSalaryQueryValidator _companySalary = new();
    private readonly UpdateJobSearchPreferencesRequestValidator _preferences = new();
    private readonly UpdateJobSearchLimitsRequestValidator _limits = new();

    [Fact]
    public void A_Minimal_Search_Is_Valid()
    {
        _search.Validate(new SearchJobsQuery("backend developer istanbul")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_Fully_Specified_Search_Is_Valid()
    {
        var query = new SearchJobsQuery("x", "cursor", 20, "TR", "tr", "İstanbul", JobSearchDatePosted.Week, true,
            "FullTime,Intern", "NoDegree,under3yearsexperience", 50, "BeeBe,Dice");

        _search.Validate(query).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void The_Query_Is_Required(string query)
    {
        _search.Validate(new SearchJobsQuery(query)).Errors.ShouldContain(e => e.PropertyName == nameof(SearchJobsQuery.Query));
    }

    [Fact]
    public void The_Query_Is_Bounded()
    {
        _search.Validate(new SearchJobsQuery(new string('a', 201))).Errors.ShouldContain(e => e.PropertyName == nameof(SearchJobsQuery.Query));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Num_Pages_Outside_The_Providers_Range_Is_Refused(int numPages)
    {
        _search.Validate(new SearchJobsQuery("x", NumPages: numPages)).Errors
            .ShouldContain(e => e.PropertyName == nameof(SearchJobsQuery.NumPages));
    }

    [Theory]
    [InlineData("TRK")]
    [InlineData("t")]
    [InlineData("t1")]
    public void Country_Must_Be_Two_Letters(string country)
    {
        _search.Validate(new SearchJobsQuery("x", Country: country)).Errors
            .ShouldContain(e => e.PropertyName == nameof(SearchJobsQuery.Country));
    }

    [Theory]
    [InlineData("t")]
    [InlineData("engl")]
    [InlineData("e1")]
    public void Language_Must_Be_Two_Or_Three_Letters(string language)
    {
        _search.Validate(new SearchJobsQuery("x", Language: language)).Errors
            .ShouldContain(e => e.PropertyName == nameof(SearchJobsQuery.Language));
    }

    [Fact]
    public void Employment_Types_Must_Be_Known_Names()
    {
        var result = _search.Validate(new SearchJobsQuery("x", EmploymentTypes: "FullTime,Freelance"));

        var error = result.Errors.ShouldHaveSingleItem();
        error.PropertyName.ShouldBe(nameof(SearchJobsQuery.EmploymentTypes));
        error.ErrorMessage.ShouldContain("FullTime, Contractor, PartTime, Intern");
    }

    [Fact]
    public void Job_Requirements_Must_Be_Known_Names()
    {
        _search.Validate(new SearchJobsQuery("x", JobRequirements: "no_degree")).Errors
            .ShouldContain(e => e.PropertyName == nameof(SearchJobsQuery.JobRequirements));
        _search.Validate(new SearchJobsQuery("x", JobRequirements: "NoDegree,NoExperience")).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Radius_Is_Bounded(int radius)
    {
        _search.Validate(new SearchJobsQuery("x", Radius: radius)).Errors.ShouldContain(e => e.PropertyName == nameof(SearchJobsQuery.Radius));
    }

    [Fact]
    public void Excluded_Publishers_Are_Bounded_In_Count_And_Length()
    {
        var eleven = string.Join(',', Enumerable.Range(1, 11).Select(i => $"p{i}"));
        _search.Validate(new SearchJobsQuery("x", ExcludeJobPublishers: eleven)).Errors
            .ShouldContain(e => e.PropertyName == nameof(SearchJobsQuery.ExcludeJobPublishers));
        _search.Validate(new SearchJobsQuery("x", ExcludeJobPublishers: new string('p', 61))).Errors
            .ShouldContain(e => e.PropertyName == nameof(SearchJobsQuery.ExcludeJobPublishers));
    }

    [Fact]
    public void Details_Need_Between_One_And_Twenty_Well_Formed_Ids()
    {
        _details.Validate(new GetJobDetailsQuery("")).IsValid.ShouldBeFalse();
        _details.Validate(new GetJobDetailsQuery("a==,b==")).IsValid.ShouldBeTrue();
        _details.Validate(new GetJobDetailsQuery(string.Join(',', Enumerable.Range(1, 21).Select(i => $"id{i}")))).IsValid.ShouldBeFalse();
        _details.Validate(new GetJobDetailsQuery("has space")).IsValid.ShouldBeFalse();
        // Real v5 ids are ~400-character tokens; the bound is well above them.
        _details.Validate(new GetJobDetailsQuery(new string('x', 402) + "," + new string('y', 402))).IsValid.ShouldBeTrue();
        _details.Validate(new GetJobDetailsQuery(new string('x', 1001))).IsValid.ShouldBeFalse();
        _details.Validate(new GetJobDetailsQuery("x", Country: "TUR")).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Salary_Lookups_Need_A_Title_And_A_Location()
    {
        _salary.Validate(new EstimatedSalaryQuery("", "Istanbul")).IsValid.ShouldBeFalse();
        _salary.Validate(new EstimatedSalaryQuery("backend developer", "")).IsValid.ShouldBeFalse();
        _salary.Validate(new EstimatedSalaryQuery("backend developer", "Istanbul")).IsValid.ShouldBeTrue();
        _salary.Validate(new EstimatedSalaryQuery(new string('t', 121), "Istanbul")).IsValid.ShouldBeFalse();
        _salary.Validate(new EstimatedSalaryQuery("x", "y", (JobSearchLocationType)99)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Company_Salary_Lookups_Need_A_Company_And_A_Title_And_Location_Is_Optional()
    {
        _companySalary.Validate(new CompanyJobSalaryQuery("", "x")).IsValid.ShouldBeFalse();
        _companySalary.Validate(new CompanyJobSalaryQuery("Trendyol", "")).IsValid.ShouldBeFalse();
        _companySalary.Validate(new CompanyJobSalaryQuery("Trendyol", "software engineer")).IsValid.ShouldBeTrue();
        _companySalary.Validate(new CompanyJobSalaryQuery("Trendyol", "x", YearsOfExperience: (JobSearchExperienceRange)99)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Preferences_Follow_The_Same_Code_Rules_And_Allow_All_Nulls()
    {
        _preferences.Validate(new UpdateJobSearchPreferencesRequest(null, null, null, null, null)).IsValid.ShouldBeTrue();
        _preferences.Validate(new UpdateJobSearchPreferencesRequest("TRK", null, null, null, null)).IsValid.ShouldBeFalse();
        _preferences.Validate(new UpdateJobSearchPreferencesRequest("tr", "e", null, null, null)).IsValid.ShouldBeFalse();
        _preferences.Validate(new UpdateJobSearchPreferencesRequest("tr", "en", "İstanbul", JobSearchDatePosted.Month, true)).IsValid.ShouldBeTrue();
        _preferences.Validate(new UpdateJobSearchPreferencesRequest(null, null, null, (JobSearchDatePosted)42, null)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Limits_Are_Bounded_By_The_Providers_Maxima()
    {
        _limits.Validate(new UpdateJobSearchLimitsRequest(null, null, null)).IsValid.ShouldBeTrue();
        _limits.Validate(new UpdateJobSearchLimitsRequest(0, 1, 1)).IsValid.ShouldBeTrue();
        _limits.Validate(new UpdateJobSearchLimitsRequest(201, null, null)).IsValid.ShouldBeFalse();
        _limits.Validate(new UpdateJobSearchLimitsRequest(null, 21, null)).IsValid.ShouldBeFalse();
        _limits.Validate(new UpdateJobSearchLimitsRequest(null, null, 0)).IsValid.ShouldBeFalse();
    }
}
