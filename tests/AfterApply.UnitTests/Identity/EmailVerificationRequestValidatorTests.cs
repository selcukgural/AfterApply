using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Identity.Validators;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

/// <summary>The verification code is exactly six digits (2026-09-24); anything else is refused
/// before it can spend one of the code's five attempts.</summary>
public class EmailVerificationRequestValidatorTests
{
    private readonly VerifyEmailRequestValidator _verify = new();
    private readonly ResendVerificationCodeRequestValidator _resend = new();

    [Theory]
    [InlineData("000000")]
    [InlineData("123456")]
    public void Accepts_Six_Digits(string code) =>
        _verify.Validate(new VerifyEmailRequest("ticket", code)).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    [InlineData(" 123456")]
    [InlineData("１２３４５６")]
    public void Refuses_Anything_But_Six_Ascii_Digits(string code) =>
        _verify.Validate(new VerifyEmailRequest("ticket", code)).IsValid.ShouldBeFalse();

    [Fact]
    public void Requires_The_Ticket()
    {
        _verify.Validate(new VerifyEmailRequest("", "123456")).IsValid.ShouldBeFalse();
        _resend.Validate(new ResendVerificationCodeRequest("")).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Refuses_An_Oversized_Ticket() =>
        _resend.Validate(new ResendVerificationCodeRequest(new string('a', 129))).IsValid.ShouldBeFalse();
}
