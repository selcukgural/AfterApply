namespace AfterApply.Application.EmailIntegrations;

/// <summary>Port to the LLM that classifies an email's application-status signal (subject/snippet
/// only, never persisted — see EmailSuggestion). Lets EmailIntegrationService be unit-tested with a
/// fake implementation instead of calling OpenAI — the real implementation
/// (OpenAiEmailClassificationProvider, Infrastructure layer) is exercised manually once a real key
/// is configured. Tried only after RuleBasedEmailClassifier returns "NoMatch" for a given email.</summary>
public interface IEmailClassificationProvider
{
    Task<EmailClassificationResult> ClassifyAsync(string subject, string snippet, CancellationToken cancellationToken);
}
