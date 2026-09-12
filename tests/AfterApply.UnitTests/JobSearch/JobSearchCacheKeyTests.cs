using AfterApply.Domain.JobSearch;
using AfterApply.Infrastructure.JobSearch;
using Shouldly;

namespace AfterApply.UnitTests.JobSearch;

public class JobSearchCacheKeyTests
{
    [Fact]
    public void Free_Text_Is_Case_Whitespace_And_Turkish_I_Insensitive()
    {
        JobSearchCacheKey.NormalizeText("  Backend   Developer  İstanbul ")
            .ShouldBe(JobSearchCacheKey.NormalizeText("backend developer istanbul"));
        JobSearchCacheKey.NormalizeText("YAZILIM").ShouldBe(JobSearchCacheKey.NormalizeText("yazılım"));
    }

    [Fact]
    public void Codes_Are_Lower_Cased_And_Blank_Is_Absent()
    {
        JobSearchCacheKey.NormalizeCode("TR").ShouldBe("tr");
        JobSearchCacheKey.NormalizeCode("  ").ShouldBeNull();
    }

    [Fact]
    public void Lists_Ignore_Order_Duplicates_And_Case()
    {
        JobSearchCacheKey.NormalizeList(["Dice", "BeeBe", "dice"]).ShouldBe(JobSearchCacheKey.NormalizeList(["beebe", "DICE"]));
        JobSearchCacheKey.NormalizeList([]).ShouldBeNull();
    }

    [Fact]
    public void Absent_And_Empty_Parameters_Hash_The_Same()
    {
        var withNulls = JobSearchCacheKey.Canonical(JobSearchOperation.Search, [new("query", "x"), new("language", null), new("cursor", "")]);
        var without = JobSearchCacheKey.Canonical(JobSearchOperation.Search, [new("query", "x")]);

        withNulls.ShouldBe(without);
        JobSearchCacheKey.Hash(withNulls).ShouldBe(JobSearchCacheKey.Hash(without));
    }

    [Fact]
    public void Parameter_Order_Does_Not_Matter_But_Values_Do()
    {
        var a = JobSearchCacheKey.Canonical(JobSearchOperation.Search, [new("query", "x"), new("country", "tr")]);
        var b = JobSearchCacheKey.Canonical(JobSearchOperation.Search, [new("country", "tr"), new("query", "x")]);
        var c = JobSearchCacheKey.Canonical(JobSearchOperation.Search, [new("country", "de"), new("query", "x")]);

        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }

    [Fact]
    public void Language_Present_Versus_Absent_Is_A_Different_Search()
    {
        var none = JobSearchCacheKey.Canonical(JobSearchOperation.Search, [new("query", "x"), new("country", "tr")]);
        var turkish = JobSearchCacheKey.Canonical(JobSearchOperation.Search, [new("query", "x"), new("country", "tr"), new("language", "tr")]);

        none.ShouldNotBe(turkish);
    }

    [Fact]
    public void Cursor_And_Page_Count_Change_The_Key()
    {
        var page1 = JobSearchCacheKey.Canonical(JobSearchOperation.Search, [new("query", "x"), new("num_pages", "1")]);
        var page3 = JobSearchCacheKey.Canonical(JobSearchOperation.Search, [new("query", "x"), new("num_pages", "3")]);
        var next = JobSearchCacheKey.Canonical(JobSearchOperation.Search, [new("query", "x"), new("num_pages", "1"), new("cursor", "abc")]);

        page1.ShouldNotBe(page3);
        page1.ShouldNotBe(next);
    }

    [Fact]
    public void Operations_Never_Collide()
    {
        var search = JobSearchCacheKey.Canonical(JobSearchOperation.Search, [new("job_title", "x")]);
        var salary = JobSearchCacheKey.Canonical(JobSearchOperation.EstimatedSalary, [new("job_title", "x")]);

        JobSearchCacheKey.Hash(search).ShouldNotBe(JobSearchCacheKey.Hash(salary));
    }

    [Fact]
    public void The_Hash_Is_64_Lower_Case_Hex_Characters()
    {
        var hash = JobSearchCacheKey.Hash("Search|query=x");

        hash.Length.ShouldBe(64);
        hash.ShouldAllBe(c => "0123456789abcdef".Contains(c));
    }
}
