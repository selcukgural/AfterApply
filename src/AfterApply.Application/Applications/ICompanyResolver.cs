namespace AfterApply.Application.Applications;

public interface ICompanyResolver
{
    Task<Guid> ResolveOrCreateAsync(string companyName, CancellationToken cancellationToken, string? linkedInUrl = null);
}
