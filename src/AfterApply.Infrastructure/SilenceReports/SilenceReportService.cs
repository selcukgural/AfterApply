using AfterApply.Infrastructure.Companies;
using System.Security.Cryptography;
using System.Text;
using AfterApply.Application.SilenceReports;
using AfterApply.Domain.SilenceReports;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AfterApply.Infrastructure.SilenceReports;

internal sealed class SilenceReportService(
    AppDbContext dbContext,
    IConnectionMultiplexer redis,
    IOptions<SilenceReportOptions> options,
    CompanyVisibility visibility)
    : ISilenceReportService
{
    /// <summary>A random secret generated once and kept in Redis beside the keys it salts, so the
    /// repeat keys are not a plain hash of an address (the IPv4 space is small enough to hash
    /// exhaustively). If it is ever evicted a new one is made; the only effect is that running
    /// repeat blocks lapse early.</summary>
    internal const string PepperKey = "silence-report:pepper";

    internal const string RepeatKeyPrefix = "silence-report:repeat:";

    public async Task<bool> SubmitAsync(string slug, SubmitSilenceReportRequest request, string requesterKey,
        CancellationToken cancellationToken)
    {
        // Reports reach a company through its public page, which only a listed company has.
        var companyId = await visibility.Listed(dbContext.Companies)
            .Where(c => c.Slug == slug)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (companyId is null)
        {
            return false;
        }

        // One report per company per connection per RepeatDays. SET NX with a TTL is the whole
        // check and the claim at once, so two concurrent submissions cannot both pass.
        var redisDb = redis.GetDatabase();
        var repeatKey = RepeatKeyPrefix + await RepeatHashAsync(redisDb, requesterKey, companyId.Value);
        var claimed = await redisDb.StringSetAsync(repeatKey, "1", TimeSpan.FromDays(options.Value.RepeatDays), When.NotExists);
        if (!claimed)
        {
            throw new SilenceReportRecentException();
        }

        try
        {
            // The validator has already run (WithValidation on the endpoint).
            var report = SilenceReport.Create(companyId.Value, request.Stage!.Value, request.Wait!.Value,
                request.PromiseGiven, request.Locale!, request.Source, DateTimeOffset.UtcNow);
            dbContext.SilenceReports.Add(report);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Nothing was stored, so nothing should be blocked: let them try again.
            await redisDb.KeyDeleteAsync(repeatKey);
            throw;
        }

        return true;
    }

    private static async Task<string> RepeatHashAsync(IDatabase redisDb, string requesterKey, Guid companyId)
    {
        var candidate = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await redisDb.StringSetAsync(PepperKey, candidate, when: When.NotExists);
        var pepper = (string?)await redisDb.StringGetAsync(PepperKey) ?? candidate;

        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), Encoding.UTF8.GetBytes($"{requesterKey}|{companyId}"));
        return Convert.ToHexStringLower(mac);
    }
}
