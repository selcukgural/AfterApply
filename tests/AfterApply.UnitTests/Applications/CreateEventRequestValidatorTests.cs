using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Applications.Validators;
using AfterApply.Application.Localization;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.Applications;

public class CreateEventRequestValidatorTests
{
    private readonly CreateEventRequestValidator _validator = new(new KeyEchoLocalizer());

    private static CreateEventRequest Request(string? metadata = null,
        ApplicationEventType type = ApplicationEventType.InterviewScheduled) =>
        new(type, DateTimeOffset.UtcNow, Source.Manual, metadata);

    [Fact]
    public void Accepts_An_Event_With_No_Metadata()
    {
        _validator.Validate(Request()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Accepts_A_Json_Object()
    {
        _validator.Validate(Request("""{"note":"Zoom üzerinden ayarlandı"}""")).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Teknik mülakat Zoom üzerinden ayarlandı")]
    [InlineData("{not json")]
    [InlineData("{\"note\":}")]
    public void Rejects_Metadata_That_Is_Not_Json(string metadata)
    {
        // Metadata is a jsonb column: without this rule these reach Postgres and surface as a 500
        // rather than a 400. The "add an event" form hit exactly this on its first real run.
        _validator.Validate(Request(metadata)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Still_Refuses_A_Hand_Written_StatusChanged_Event()
    {
        // Status moves have to go through the status endpoint, which writes real history.
        _validator.Validate(Request(type: ApplicationEventType.StatusChanged)).IsValid.ShouldBeFalse();
    }

    // Same shape the other validator tests use — there is no mocking library in this project.
    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
