namespace AfterApply.Application.EmailIntegrations;

public sealed record EmailJobExtractionResult(string CompanyName, string JobTitle, string? Location, string? Description);

/// <summary>Port to the LLM that reads company/job-title/location/description out of a forwarded
/// email for a job that isn't registered in the app yet — only tried after EmailForwardingService
/// finds no matching Application AND a classification pass finds an actual status signal (see
/// DECISIONS.md "Eşleşmeyen email'ler gösterilmiyor"; extraction never runs on signal-less mail).
/// Returns null when the model isn't confident it read a real job-application email, or couldn't
/// read a company name and job title — the caller skips silently in that case, same as an
/// unmatched email today.</summary>
public interface IEmailJobExtractionProvider
{
    Task<EmailJobExtractionResult?> ExtractAsync(string subject, string snippet, CancellationToken cancellationToken);
}
