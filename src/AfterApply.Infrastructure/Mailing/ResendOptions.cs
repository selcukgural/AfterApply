namespace AfterApply.Infrastructure.Mailing;

/// <summary>Config for Resend (https://resend.com), our outbound transactional email provider.
/// Null/empty ApiKey means sending stays inert — same "inert until set" pattern as
/// AfterApply.Infrastructure.OpenAi.OpenAiOptions — so a missing key degrades to "email not
/// sent, logged as a warning" instead of failing the caller's request.</summary>
public sealed class ResendOptions
{
    public string? ApiKey { get; init; }

    public string FromEmail { get; init; } = "no-reply@mail.ekariyerim.com";

    public string FromName { get; init; } = "e-kariyerim";
}
