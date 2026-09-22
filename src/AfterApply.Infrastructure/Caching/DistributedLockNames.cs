namespace AfterApply.Infrastructure.Caching;

/// <summary>The Redis key a distributed lock lives under. Registered with the deployment's key
/// prefix so lock names are spelled in one place and never collide with cache entries or with
/// another deployment on the same Redis.</summary>
public sealed class DistributedLockNames(string keyPrefix)
{
    public string CompanyEnrichment(Guid companyId) => $"{keyPrefix}lock:enrich:{companyId}";

    public string AtsJobEnrichment(Guid jobId) => $"{keyPrefix}lock:enrich-job:{jobId}";
}
