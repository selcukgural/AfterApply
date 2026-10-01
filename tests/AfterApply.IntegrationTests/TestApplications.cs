using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AfterApply.IntegrationTests;

/// <summary>
/// The API stamps an application's CreatedAt with the moment of the request, so a test that seeds
/// "an application sent 45 days ago" through it gets a row entered 45 days late — which the
/// cross-user figures leave out (AggregateEligibility, DECISIONS.md 2026-10-01). A test about
/// something other than that rule moves CreatedAt back to the day of applying: the user who logged
/// it as it happened.
/// </summary>
public static class TestApplications
{
    public static async Task EnteredOnAsync(IServiceProvider services, Guid applicationId, DateTimeOffset createdAt)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Applications
            .Where(a => a.Id == applicationId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.CreatedAt, createdAt));
    }
}
