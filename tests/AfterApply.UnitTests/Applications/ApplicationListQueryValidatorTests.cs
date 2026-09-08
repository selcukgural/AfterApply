using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Applications.Validators;
using AfterApply.Domain.Applications;
using Shouldly;

namespace AfterApply.UnitTests.Applications;

/// <summary>
/// The two list queries. They deliberately do not share a validator: the same field name means a
/// different thing in each — <c>PageSize</c> counts applications in one and companies in the other —
/// and the sort enums have no values in common.
/// </summary>
public class ApplicationListQueryValidatorTests
{
    private static readonly GetApplicationsQueryValidator FlatValidator = new();
    private static readonly GetGroupedApplicationsQueryValidator GroupedValidator = new();

    [Fact]
    public void The_Flat_List_Accepts_A_Company_Narrowing()
    {
        var result = FlatValidator.Validate(new GetApplicationsQuery(CompanyId: Guid.NewGuid()));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void An_Empty_Guid_Is_Not_A_Company()
    {
        // Guid.Empty round-trips through a query string as a perfectly well-formed value, so it
        // would otherwise reach the query and silently match nothing.
        var result = FlatValidator.Validate(new GetApplicationsQuery(CompanyId: Guid.Empty));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void No_Company_Narrowing_Is_The_Normal_Case()
    {
        FlatValidator.Validate(new GetApplicationsQuery()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void The_Flat_List_Bounds_Its_Page_Size(int pageSize)
    {
        FlatValidator.Validate(new GetApplicationsQuery(PageSize: pageSize)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void The_Company_View_Has_A_Lower_Page_Size_Ceiling()
    {
        // Fifty companies is already a long page once each one draws its applications underneath.
        GroupedValidator.Validate(new GetGroupedApplicationsQuery(PageSize: 50)).IsValid.ShouldBeTrue();
        GroupedValidator.Validate(new GetGroupedApplicationsQuery(PageSize: 51)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Both_Views_Refuse_A_Page_Below_The_First()
    {
        FlatValidator.Validate(new GetApplicationsQuery(Page: 0)).IsValid.ShouldBeFalse();
        GroupedValidator.Validate(new GetGroupedApplicationsQuery(Page: 0)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Both_Views_Refuse_A_Status_Outside_The_Enum()
    {
        var bogus = (ApplicationStatus)int.MaxValue;

        FlatValidator.Validate(new GetApplicationsQuery(Status: bogus)).IsValid.ShouldBeFalse();
        GroupedValidator.Validate(new GetGroupedApplicationsQuery(Status: bogus)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void The_Company_View_Defaults_To_Ordering_By_Last_Activity()
    {
        var query = new GetGroupedApplicationsQuery();

        query.SortBy.ShouldBe(CompanyGroupSortBy.LastActivity);
        // Fully qualified: Shouldly ships a SortDirection of its own.
        query.SortDirection.ShouldBe(AfterApply.Application.Applications.Contracts.SortDirection.Descending);
        GroupedValidator.Validate(query).IsValid.ShouldBeTrue();
    }
}
