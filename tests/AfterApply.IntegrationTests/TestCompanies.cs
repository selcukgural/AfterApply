using DomainApplication = AfterApply.Domain.Applications.Application;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Identity;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AfterApply.IntegrationTests;

/// <summary>
/// Since 2026-09-24 a company has a public page only once it is listed — a contribution, or three
/// different people who applied to it (CompanyVisibility). A test about something else on that page
/// lists the company this way first: three bare accounts with one application each.
/// </summary>
public static class TestCompanies
{
    public const int ListingApplicants = 3;

    public static async Task MakeListedAsync(IServiceProvider services, Guid companyId)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < ListingApplicants; i++)
        {
            var email = $"lister-{companyId:N}-{i}@example.com";
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, CreatedAt = now };
            var created = await users.CreateAsync(user);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(string.Join(", ", created.Errors.Select(e => e.Code)));
            }

            db.Applications.Add(DomainApplication.Create(user.Id, companyId, "Engineer", null, null,
                EmploymentType.FullTime, now.AddDays(-1), Source.Manual, notes: null, now));
        }

        await db.SaveChangesAsync();
    }
}
