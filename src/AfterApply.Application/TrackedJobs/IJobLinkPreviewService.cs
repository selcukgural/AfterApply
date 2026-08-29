using AfterApply.Application.TrackedJobs.Contracts;

namespace AfterApply.Application.TrackedJobs;

/// <summary>
/// Best-effort: never throws for a bad, unsupported, or unreachable URL — just returns nulls for
/// whatever it couldn't resolve. Only linkedin.com and kariyer.net (with subdomains) are ever
/// fetched; see the Infrastructure implementation for why.
/// </summary>
public interface IJobLinkPreviewService
{
    Task<TrackedJobLinkPreviewResponse> ResolveAsync(string jobUrl, CancellationToken cancellationToken);
}
