using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Identity.Validators;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

/// <summary>Only Extension tokens can be issued (2026-09-24): a session-equivalent token would
/// outlive the session that minted it by 90 days.</summary>
public class CreatePersonalAccessTokenRequestValidatorTests
{
    private readonly CreatePersonalAccessTokenRequestValidator _validator = new();

    [Fact]
    public void Accepts_An_Extension_Token_Which_Is_Also_The_Default()
    {
        _validator.Validate(new CreatePersonalAccessTokenRequest("Chrome Extension")).IsValid.ShouldBeTrue();
        _validator.Validate(new CreatePersonalAccessTokenRequest("Chrome Extension", PersonalAccessTokenScope.Extension)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Refuses_A_Full_Token() =>
        _validator.Validate(new CreatePersonalAccessTokenRequest("Scripting", PersonalAccessTokenScope.Full)).IsValid.ShouldBeFalse();

    [Fact]
    public void Refuses_A_Scope_The_Enum_Does_Not_Define() =>
        _validator.Validate(new CreatePersonalAccessTokenRequest("Odd", (PersonalAccessTokenScope)7)).IsValid.ShouldBeFalse();
}
