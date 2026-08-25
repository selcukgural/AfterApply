using AfterApply.Application.Common;
using AfterApply.Application.Matching;
using AfterApply.Application.Matching.Contracts;
using AfterApply.Domain.Matching;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.Matching;

internal sealed class JobMatchingService(AppDbContext dbContext, IJobMatchingProvider provider) : IJobMatchingService
{
    public async Task<CandidateProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.CandidateProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return profile is null ? null : new CandidateProfileResponse(profile.CvText, profile.UpdatedAt);
    }

    public async Task<CandidateProfileResponse> UpdateProfileAsync(Guid userId, UpdateCandidateProfileRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var profile = await dbContext.CandidateProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            profile = CandidateProfile.Create(userId, request.CvText, now);
            dbContext.CandidateProfiles.Add(profile);
        }
        else
        {
            profile.UpdateCv(request.CvText, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CandidateProfileResponse(profile.CvText, profile.UpdatedAt);
    }

    public async Task<JobMatchResponse?> GetMatchAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var match = await dbContext.JobMatches
            .FirstOrDefaultAsync(m => m.UserId == userId && m.ApplicationId == applicationId, cancellationToken);

        return match is null ? null : ToResponse(match);
    }

    public async Task<JobMatchResponse?> ComputeMatchAsync(Guid userId, Guid applicationId, ComputeJobMatchRequest request, CancellationToken cancellationToken)
    {
        var applicationExists = await dbContext.Applications
            .AnyAsync(a => a.Id == applicationId && a.UserId == userId, cancellationToken);

        if (!applicationExists)
        {
            return null;
        }

        var profile = await dbContext.CandidateProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            throw new CodedException("MATCHING_PROFILE_REQUIRED", "Set your CV before computing a job match.");
        }

        var existingMatch = await dbContext.JobMatches
            .FirstOrDefaultAsync(m => m.UserId == userId && m.ApplicationId == applicationId, cancellationToken);

        if (existingMatch is not null && existingMatch.MatchesInputs(profile.CvText, request.JobDescription))
        {
            return ToResponse(existingMatch);
        }

        var result = await provider.MatchAsync(profile.CvText, request.JobDescription, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (existingMatch is null)
        {
            existingMatch = JobMatch.Create(userId, applicationId, profile.CvText, request.JobDescription,
                result.Score, result.StrongMatches, result.Missing, result.Recommendation, now);
            dbContext.JobMatches.Add(existingMatch);
        }
        else
        {
            existingMatch.Recompute(profile.CvText, request.JobDescription,
                result.Score, result.StrongMatches, result.Missing, result.Recommendation, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(existingMatch);
    }

    private static JobMatchResponse ToResponse(JobMatch match) => new(
        match.ApplicationId, match.Score, match.StrongMatches, match.Missing, match.Recommendation, match.ComputedAt);
}
