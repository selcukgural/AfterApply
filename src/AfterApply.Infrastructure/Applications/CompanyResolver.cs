using AfterApply.Application.Applications;
using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.Applications;

internal sealed class CompanyResolver(AppDbContext dbContext) : ICompanyResolver
{
    public async Task<Guid> ResolveOrCreateAsync(string companyName, CancellationToken cancellationToken)
    {
        var normalizedName = CompanyNameNormalizer.Normalize(companyName);

        var existingId = await dbContext.Companies
            .Where(c => c.NormalizedName == normalizedName)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingId is not null)
        {
            return existingId.Value;
        }

        var company = Company.Create(companyName, DateTimeOffset.UtcNow);
        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);

        return company.Id;
    }
}
