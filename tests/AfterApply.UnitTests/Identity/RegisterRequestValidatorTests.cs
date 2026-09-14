using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Identity.Validators;
using AfterApply.Application.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

/// <summary>
/// The sign-up form stopped asking for a name on 2026-09-14 (the account works without one, and
/// the name was one more field between a visitor and a dashboard that converted nobody). The
/// validator has to accept what the form now sends — empty strings — without accepting a null
/// that the NOT NULL column would reject further down.
/// </summary>
public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new(new KeyEchoLocalizer());

    [Fact]
    public void Accepts_A_Registration_Without_A_Name()
    {
        var result = _validator.Validate(new RegisterRequest("ada@example.com", "correct horse battery", "", "", true));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Still_Accepts_A_Registration_With_A_Name()
    {
        var result = _validator.Validate(new RegisterRequest("ada@example.com", "correct horse battery", "Ada", "Lovelace", true));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_A_Null_Name_Because_The_Column_Is_Not_Null()
    {
        var result = _validator.Validate(new RegisterRequest("ada@example.com", "correct horse battery", null!, null!, true));

        result.Errors.Select(e => e.PropertyName).Distinct()
            .ShouldBe([nameof(RegisterRequest.FirstName), nameof(RegisterRequest.LastName)], ignoreOrder: true);
    }

    [Fact]
    public void Still_Caps_A_Name_At_The_Column_Width()
    {
        var result = _validator.Validate(new RegisterRequest("ada@example.com", "correct horse battery", new string('a', 101), "", true));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(RegisterRequest.FirstName));
    }

    [Fact]
    public void Still_Requires_Consent()
    {
        var result = _validator.Validate(new RegisterRequest("ada@example.com", "correct horse battery", "", "", false));

        result.Errors.ShouldContain(e => e.ErrorMessage == "VALIDATION_CONSENT_REQUIRED");
    }

    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
