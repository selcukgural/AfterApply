using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Identity.Validators;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

/// <summary>
/// The profile page follows the sign-up rule of 2026-09-14: a name is optional, so the form may
/// send an empty first or last name (someone who registered without one and only fills in the
/// other, or clears theirs). A null still fails because the columns are NOT NULL.
/// </summary>
public class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator _validator = new();

    [Fact]
    public void Accepts_Empty_Names()
    {
        var result = _validator.Validate(new UpdateProfileRequest("", ""));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Accepts_Only_One_Name()
    {
        var result = _validator.Validate(new UpdateProfileRequest("Ada", ""));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_A_Null_Name_Because_The_Column_Is_Not_Null()
    {
        var result = _validator.Validate(new UpdateProfileRequest(null!, null!));

        result.Errors.Select(e => e.PropertyName).Distinct()
            .ShouldBe([nameof(UpdateProfileRequest.FirstName), nameof(UpdateProfileRequest.LastName)], ignoreOrder: true);
    }

    [Fact]
    public void Caps_A_Name_At_The_Column_Width()
    {
        var result = _validator.Validate(new UpdateProfileRequest(new string('a', 101), new string('b', 100)));

        result.Errors.Select(e => e.PropertyName).ShouldBe([nameof(UpdateProfileRequest.FirstName)]);
    }
}
