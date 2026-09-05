using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Identity.Validators;
using AfterApply.Application.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

public class GoogleAuthRequestValidatorTests
{
    private const string ValidVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"; // 43 chars, RFC 7636 example
    private const string ValidRedirect = "http://localhost:3000/tr/auth/google/callback";

    [Fact]
    public void Sign_In_Request_Accepts_A_Well_Formed_Body()
    {
        var result = new GoogleSignInRequestValidator().Validate(new GoogleSignInRequest("4/0Ab_code", ValidVerifier, ValidRedirect));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("has spaces has spaces has spaces has spaces has spaces")]
    public void Sign_In_Request_Rejects_A_Verifier_Outside_RFC_7636(string verifier)
    {
        var result = new GoogleSignInRequestValidator().Validate(new GoogleSignInRequest("code", verifier, ValidRedirect));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GoogleSignInRequest.CodeVerifier));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/tr/auth/google/callback")]
    [InlineData("javascript:alert(1)")]
    public void Sign_In_Request_Rejects_A_Non_Web_Redirect_Uri(string redirectUri)
    {
        var result = new GoogleSignInRequestValidator().Validate(new GoogleSignInRequest("code", ValidVerifier, redirectUri));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GoogleSignInRequest.RedirectUri));
    }

    [Fact]
    public void Signup_Request_Requires_Consent_Like_Register_Does()
    {
        var result = new GoogleSignupRequestValidator(new KeyEchoLocalizer())
            .Validate(new GoogleSignupRequest("token", "Ada", "Lovelace", ConsentAccepted: false));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == "VALIDATION_CONSENT_REQUIRED");
    }

    [Fact]
    public void Signup_Request_Requires_Both_Names_And_The_Token()
    {
        var result = new GoogleSignupRequestValidator(new KeyEchoLocalizer())
            .Validate(new GoogleSignupRequest("", "", "", ConsentAccepted: true));

        result.Errors.Select(e => e.PropertyName).Distinct()
            .ShouldBe([nameof(GoogleSignupRequest.SignupToken), nameof(GoogleSignupRequest.FirstName), nameof(GoogleSignupRequest.LastName)],
                ignoreOrder: true);
    }

    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
