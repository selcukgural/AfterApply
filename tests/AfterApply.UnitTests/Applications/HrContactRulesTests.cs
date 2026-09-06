using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Applications.Validators;
using AfterApply.Application.Localization;
using AfterApply.Domain.Common;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.Applications;

// The HR contact rules are shared by three request validators; exercised here through the update
// request, which is the one that has to accept a cleared field as well as a filled one.
public class HrContactRulesTests
{
    private readonly UpdateApplicationRequestValidator _validator;

    public HrContactRulesTests()
    {
        _validator = new UpdateApplicationRequestValidator(new KeyEchoLocalizer());
    }

    private static UpdateApplicationRequest Request(string? name = null, string? email = null, string? linkedInUrl = null) =>
        new("Backend Engineer", JobUrl: null, Location: null, EmploymentType.FullTime,
            DateTimeOffset.UtcNow.AddDays(-1), Notes: null, name, email, linkedInUrl);

    [Fact]
    public void Accepts_A_Complete_Contact()
    {
        _validator.Validate(Request("Zeynep A.", "talent@oplog.com", "https://www.linkedin.com/in/zeynep-a"))
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Accepts_An_Empty_Contact_So_A_User_Can_Clear_It()
    {
        // UpdateDetails assigns straight through, so "no contact" has to be a valid save — this is
        // the edit form, and emptying a field is how a user removes a stale contact.
        _validator.Validate(Request()).IsValid.ShouldBeTrue();
        _validator.Validate(Request(name: "", email: "", linkedInUrl: "")).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b.com, c@d.com")]
    public void Rejects_A_Malformed_Email(string email)
    {
        var result = _validator.Validate(Request(email: email));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "HrEmail");
    }

    [Theory]
    // A company page, not a person — the field is labelled and rendered as a profile.
    [InlineData("https://www.linkedin.com/company/oplog/")]
    // Another host entirely, and one that merely ends in the right letters.
    [InlineData("https://evil.example/in/zeynep-a")]
    [InlineData("https://notlinkedin.com/in/zeynep-a")]
    // Not https: the value is rendered as an href.
    [InlineData("http://www.linkedin.com/in/zeynep-a")]
    [InlineData("javascript:alert(1)")]
    public void Rejects_Anything_That_Is_Not_A_LinkedIn_Profile_Url(string url)
    {
        var result = _validator.Validate(Request(linkedInUrl: url));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "HrLinkedInUrl");
    }

    [Fact]
    public void Accepts_A_Locale_Subdomain_Profile_Url()
    {
        _validator.Validate(Request(linkedInUrl: "https://tr.linkedin.com/in/zeynep-a")).IsValid.ShouldBeTrue();
    }

    // Same shape the identity validator tests use — there is no mocking library in this project.
    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
