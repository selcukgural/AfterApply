namespace AfterApply.Infrastructure.Matching;

public sealed class OpenAiOptions
{
    public string? ApiKey { get; init; }

    public string Model { get; init; } = "gpt-4o-mini";
}
