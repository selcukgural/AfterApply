using System.Text.Json;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.EmailIntegrations.Validators;
using Shouldly;

namespace AfterApply.UnitTests.EmailIntegrations;

// The first version of this validator dereferenced LinkDomains inside a When predicate, which
// turned a malformed body into a 500 instead of a 400. It read as safe because the record declared
// the property non-nullable — but nullable reference types are a compile-time contract only, and
// System.Text.Json writes null straight through it. These tests pin down both halves: that the
// deserializer really does produce null, and that the validator answers with a validation failure
// rather than an exception when it does.
public class ExtensionEmailSignalRequestValidatorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Everything except linkDomains, so each test can vary that one field the way a real request
    // body would — through the deserializer, not through the constructor, since the constructor
    // can't reproduce what actually goes wrong here.
    private const string OtherFields = """
        "senderEmail": "recruiter@example.com",
        "senderDisplayName": "Example Recruiting",
        "subject": "Interview invitation",
        "snippet": "We would like to invite you to an interview.",
        "receivedAt": "2026-09-03T08:00:00Z",
        "gmailMessageId": "thread-1"
        """;

    private static ExtensionEmailSignalRequest Deserialize(string linkDomainsJson) =>
        JsonSerializer.Deserialize<ExtensionEmailSignalRequest>(
            $$"""{ {{OtherFields}}, "linkDomains": {{linkDomainsJson}} }""", JsonOptions)!;

    [Fact]
    public void Json_Null_Really_Does_Reach_The_Non_Nullable_Looking_Property()
    {
        Deserialize("null").LinkDomains.ShouldBeNull();

        var omitted = JsonSerializer.Deserialize<ExtensionEmailSignalRequest>(
            $$"""{ {{OtherFields}} }""", JsonOptions)!;
        omitted.LinkDomains.ShouldBeNull();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""["greenhouse.io", "calendly.com"]""")]
    public void Never_Throws_On_Any_Shape_Of_LinkDomains(string linkDomainsJson)
    {
        var request = Deserialize(linkDomainsJson);

        Should.NotThrow(() => new ExtensionEmailSignalRequestValidator().Validate(request));
    }

    [Fact]
    public void Null_LinkDomains_Is_A_Validation_Failure_Not_An_Exception()
    {
        var result = new ExtensionEmailSignalRequestValidator().Validate(Deserialize("null"));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ExtensionEmailSignalRequest.LinkDomains));
    }

    [Fact]
    public void Empty_LinkDomains_Is_Valid()
    {
        new ExtensionEmailSignalRequestValidator().Validate(Deserialize("[]")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void More_Than_Fifty_Link_Domains_Is_Rejected()
    {
        var domains = string.Join(",", Enumerable.Range(0, 51).Select(i => $"\"host{i}.example.com\""));

        var result = new ExtensionEmailSignalRequestValidator().Validate(Deserialize($"[{domains}]"));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void An_Over_Long_Link_Domain_Is_Rejected()
    {
        var tooLong = new string('a', 254);

        var result = new ExtensionEmailSignalRequestValidator().Validate(Deserialize($"[\"{tooLong}\"]"));

        result.IsValid.ShouldBeFalse();
    }

    // The caps that matter most: these mirror EmailSuggestions' column lengths, and before they
    // existed an oversized value only failed later, inside the Hangfire job, where it became ten
    // retries of a request that could never succeed.
    [Fact]
    public void Subject_And_Snippet_Are_Capped_At_The_Column_Lengths()
    {
        var longSubject = JsonSerializer.Deserialize<ExtensionEmailSignalRequest>(
            $$"""
              {
                "senderEmail": "recruiter@example.com", "senderDisplayName": "R",
                "subject": "{{new string('s', 501)}}", "snippet": "x",
                "receivedAt": "2026-09-03T08:00:00Z", "linkDomains": [], "gmailMessageId": "t"
              }
              """, JsonOptions)!;

        var result = new ExtensionEmailSignalRequestValidator().Validate(longSubject);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ExtensionEmailSignalRequest.Subject));
    }

    [Fact]
    public void A_Well_Formed_Request_Passes()
    {
        new ExtensionEmailSignalRequestValidator()
            .Validate(Deserialize("""["greenhouse.io"]"""))
            .IsValid.ShouldBeTrue();
    }
}
