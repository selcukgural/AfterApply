using AfterApply.Application.JobSearch;
using AfterApply.Application.JobSearch.Contracts;
using Shouldly;

namespace AfterApply.UnitTests.JobSearch;

public class JobSearchCsvTests
{
    [Fact]
    public void Split_Trims_Drops_Empties_And_Dedupes_Case_Insensitively()
    {
        JobSearchCsv.Split(" a , B ,, b ,a").ShouldBe(["a", "B"]);
        JobSearchCsv.Split(null).ShouldBeEmpty();
        JobSearchCsv.Split(" , ").ShouldBeEmpty();
    }

    [Fact]
    public void Split_Can_Keep_Case_For_Provider_Ids()
    {
        JobSearchCsv.Split("Ab==,ab==", ignoreCase: false).ShouldBe(["Ab==", "ab=="]);
    }

    [Fact]
    public void Enum_Lists_Parse_By_Name_Case_Insensitively()
    {
        JobSearchCsv.TryParseEnumList<JobSearchEmploymentType>("fulltime,Intern,FULLTIME", out var values, out var invalid).ShouldBeTrue();
        values.ShouldBe([JobSearchEmploymentType.FullTime, JobSearchEmploymentType.Intern]);
        invalid.ShouldBeNull();
    }

    [Fact]
    public void Enum_Lists_Refuse_Unknown_Names_And_Numbers()
    {
        JobSearchCsv.TryParseEnumList<JobSearchEmploymentType>("FullTime,Freelance", out _, out var invalid).ShouldBeFalse();
        invalid.ShouldBe("Freelance");
        JobSearchCsv.IsValidEnumList<JobSearchJobRequirement>("2").ShouldBeFalse();
        JobSearchCsv.IsValidEnumList<JobSearchJobRequirement>(null).ShouldBeTrue();
    }
}
