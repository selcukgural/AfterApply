using AfterApply.Application.CompanyReviews;
using AfterApply.Application.CompanyReviews.Contracts;
using Shouldly;

namespace AfterApply.UnitTests.CompanyReviews;

public class ContributionPagingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static ContributionStamp At(ContributionKind kind, int minutesAgo, Guid? id = null) =>
        new(kind, id ?? Guid.CreateVersion7(), T0.AddMinutes(-minutesAgo));

    [Fact]
    public void Newest_First_Across_Kinds()
    {
        var review = At(ContributionKind.Review, 30);
        var salary = At(ContributionKind.Salary, 10);
        var experience = At(ContributionKind.Experience, 20);

        var (page, total) = ContributionPaging.Page([review, salary, experience], page: 1, pageSize: 10);

        total.ShouldBe(3);
        page.Select(s => s.Kind).ShouldBe([ContributionKind.Salary, ContributionKind.Experience, ContributionKind.Review]);
    }

    [Fact]
    public void Same_Instant_Falls_Back_To_Id_Descending()
    {
        var older = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var newer = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var stamps = new[] { At(ContributionKind.Review, 5, older), At(ContributionKind.Salary, 5, newer) };

        var (page, _) = ContributionPaging.Page(stamps, 1, 10);

        page.Select(s => s.Id).ShouldBe([newer, older]);
    }

    [Fact]
    public void Pages_Are_Cut_After_Ordering()
    {
        var stamps = Enumerable.Range(0, 7).Select(i => At(ContributionKind.Review, i)).ToList();

        var (first, total) = ContributionPaging.Page(stamps, 1, 3);
        var (third, _) = ContributionPaging.Page(stamps, 3, 3);

        total.ShouldBe(7);
        first.Select(s => s.SubmittedAt).ShouldBe([T0, T0.AddMinutes(-1), T0.AddMinutes(-2)]);
        third.Count.ShouldBe(1);
        third[0].SubmittedAt.ShouldBe(T0.AddMinutes(-6));
    }

    [Fact]
    public void Past_The_End_Is_Empty_Not_An_Error()
    {
        var (page, total) = ContributionPaging.Page([At(ContributionKind.Salary, 1)], page: 5, pageSize: 10);

        total.ShouldBe(1);
        page.ShouldBeEmpty();
    }

    [Fact]
    public void Empty_Input_Is_An_Empty_First_Page()
    {
        var (page, total) = ContributionPaging.Page([], 1, 10);

        total.ShouldBe(0);
        page.ShouldBeEmpty();
    }
}
