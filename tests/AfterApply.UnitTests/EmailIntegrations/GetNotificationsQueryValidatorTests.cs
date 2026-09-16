using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.EmailIntegrations.Validators;
using Shouldly;

namespace AfterApply.UnitTests.EmailIntegrations;

public class GetNotificationsQueryValidatorTests
{
    private static readonly GetNotificationsQueryValidator Validator = new();

    [Fact]
    public void The_Defaults_Are_The_Pages_Own_Shape()
    {
        var query = new GetNotificationsQuery();

        query.Page.ShouldBe(1);
        query.PageSize.ShouldBe(10);
        Validator.Validate(query).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public void A_Page_Before_The_First_Or_A_Size_Outside_One_To_Fifty_Is_Refused(int page, int pageSize)
    {
        Validator.Validate(new GetNotificationsQuery(page, pageSize)).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 50)]
    public void Any_Page_With_A_Size_Inside_The_Ceiling_Is_Accepted(int page, int pageSize)
    {
        Validator.Validate(new GetNotificationsQuery(page, pageSize)).IsValid.ShouldBeTrue();
    }
}
