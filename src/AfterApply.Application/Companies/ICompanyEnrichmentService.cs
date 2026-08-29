namespace AfterApply.Application.Companies;

public interface ICompanyEnrichmentService
{
    Task EnrichAsync(Guid companyId, CancellationToken cancellationToken);
}
