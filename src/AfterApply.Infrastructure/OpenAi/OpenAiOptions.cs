namespace AfterApply.Infrastructure.OpenAi;

public sealed class OpenAiOptions
{
    public string? ApiKey { get; init; }

    public string Model { get; init; } = "gpt-4o-mini";
}
