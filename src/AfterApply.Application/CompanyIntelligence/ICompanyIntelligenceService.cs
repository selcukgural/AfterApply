using AfterApply.Application.CompanyIntelligence.Contracts;

namespace AfterApply.Application.CompanyIntelligence;

public interface ICompanyIntelligenceService
{
    // Null return means the company doesn't exist — mapped to 404 by the endpoint, the same
    // status code used when the CompanyIntelligence:Enabled flag is off, so the two cases are
    // indistinguishable to a caller.
    Task<CompanyIntelligenceResponse?> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken);
}
