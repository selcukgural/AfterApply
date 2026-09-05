using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Identity.Validators;
using AfterApply.Application.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

public class LinkedInAuthRequestValidatorTests
{
    private const string ValidRedirect = "https://ekariyerim.com/tr/auth/linkedin/callback";

    [Fact]
    public void Sign_In_Request_Accepts_A_Well_Formed_Body()
    {
        var result = new LinkedInSignInRequestValidator().Validate(new LinkedInSignInRequest("code", ValidRedirect));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("/tr/auth/linkedin/callback")]
    [InlineData("javascript:alert(1)")]
    public void Sign_In_Request_Rejects_A_Non_Web_Redirect_Uri(string redirectUri)
    {
        var result = new LinkedInSignInRequestValidator().Validate(new LinkedInSignInRequest("code", redirectUri));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(LinkedInSignInRequest.RedirectUri));
    }

    [Fact]
    public void Signup_Request_Accepts_A_Null_Email()
    {
        var result = new LinkedInSignupRequestValidator(new KeyEchoLocalizer())
            .Validate(new LinkedInSignupRequest("token", "Ada", "Lovelace", null, true));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Signup_Request_Rejects_A_Malformed_Email_When_One_Is_Given()
    {
        var result = new LinkedInSignupRequestValidator(new KeyEchoLocalizer())
            .Validate(new LinkedInSignupRequest("token", "Ada", "Lovelace", "not-an-email", true));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(LinkedInSignupRequest.Email));
    }

    [Fact]
    public void Signup_Request_Requires_Consent_Like_Register_Does()
    {
        var result = new LinkedInSignupRequestValidator(new KeyEchoLocalizer())
            .Validate(new LinkedInSignupRequest("token", "Ada", "Lovelace", null, ConsentAccepted: false));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == "VALIDATION_CONSENT_REQUIRED");
    }

    [Fact]
    public void Signup_Request_Requires_Both_Names_And_The_Token()
    {
        var result = new LinkedInSignupRequestValidator(new KeyEchoLocalizer())
            .Validate(new LinkedInSignupRequest("", "", "", null, ConsentAccepted: true));

        result.Errors.Select(e => e.PropertyName).Distinct()
            .ShouldBe([nameof(LinkedInSignupRequest.SignupToken), nameof(LinkedInSignupRequest.FirstName), nameof(LinkedInSignupRequest.LastName)],
                ignoreOrder: true);
    }

    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
