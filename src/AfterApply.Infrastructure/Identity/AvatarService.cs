using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.Blog;
using AfterApply.Infrastructure.Documents;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// Profile photos (DECISIONS.md 2026-09-28). The same discipline as a CV or blog image upload —
/// storage before the row, a failed row deletes the new object — plus two things of its own: only
/// the re-encoded photo is ever written (<see cref="AvatarImageProcessor"/>), and the id a URL
/// carries is random and replaced on every change, never the user id.
/// </summary>
internal sealed class AvatarService(
    AppDbContext dbContext,
    IAvatarStorage storage,
    IOptions<StorageOptions> options,
    IStringLocalizer<SharedStrings> localizer,
    ILogger<AvatarService> logger) : IAvatarService
{
    public async Task<UserProfileResponse?> UploadAsync(Guid userId, Stream content, long declaredLength,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var maxFileSizeBytes = options.Value.MaxFileSizeBytes;
        if (AvatarImageProcessor.InspectClaim(declaredLength, maxFileSizeBytes) is { } claimProblem)
        {
            throw new AvatarUploadValidationException(MessageFor(claimProblem, maxFileSizeBytes));
        }

        byte[] photo;
        try
        {
            photo = await AvatarImageProcessor.ProcessAsync(content, cancellationToken);
        }
        catch (AvatarImageRejectedException rejected)
        {
            throw new AvatarUploadValidationException(MessageFor(rejected.Problem, maxFileSizeBytes));
        }

        var objectName = $"avatars/{userId:D}/{Guid.NewGuid():N}.{AvatarImageProcessor.OutputExtension}";
        await using (var stream = new MemoryStream(photo, writable: false))
        {
            await storage.SaveAsync(objectName, stream, AvatarImageProcessor.OutputContentType, cancellationToken);
        }

        var previousObjectName = user.AvatarObjectName;
        user.AvatarObjectName = objectName;
        user.AvatarPublicId = Guid.NewGuid();
        user.AvatarUpdatedAt = DateTimeOffset.UtcNow;

        if (!await TrySaveAsync(user, cancellationToken))
        {
            // Another change to this account won the race (a second upload in another tab); its
            // photo is the one that stands, and this one was never referenced.
            await TryDeleteObjectAsync(objectName);
            return await CurrentProfileAsync(userId, cancellationToken);
        }

        if (previousObjectName is not null)
        {
            await TryDeleteObjectAsync(previousObjectName);
        }

        return UserProfiles.From(user);
    }

    public async Task<UserProfileResponse?> DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var objectName = Clear(user);
        if (!await TrySaveAsync(user, cancellationToken))
        {
            return await CurrentProfileAsync(userId, cancellationToken);
        }

        if (objectName is not null)
        {
            await TryDeleteObjectAsync(objectName);
        }

        return UserProfiles.From(user);
    }

    public async Task<UserProfileResponse?> SetShowInCommentsAsync(Guid userId, bool showInComments,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        // Turning it off takes back what readers were already handed: the URL on the comments they
        // loaded stops resolving, not just future pages. Turning it on reuses the id the owner
        // already has — nothing was handed out that needs taking back.
        if (!showInComments && user.ShowAvatarInComments && user.AvatarPublicId is not null)
        {
            user.AvatarPublicId = Guid.NewGuid();
        }

        user.ShowAvatarInComments = showInComments;
        if (!await TrySaveAsync(user, cancellationToken))
        {
            return await CurrentProfileAsync(userId, cancellationToken);
        }

        return UserProfiles.From(user);
    }

    public async Task<AvatarContent?> OpenAsync(Guid publicId, CancellationToken cancellationToken)
    {
        var objectName = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.AvatarPublicId == publicId)
            .Select(u => u.AvatarObjectName)
            .FirstOrDefaultAsync(cancellationToken);
        if (objectName is null)
        {
            return null;
        }

        var stream = await storage.OpenReadAsync(objectName, cancellationToken);
        if (stream is null)
        {
            // The object name alone: it carries no personal data beyond an id nobody sees.
            logger.LogWarning("Avatar object {ObjectName} is referenced but not stored.", objectName);
            return null;
        }

        return new AvatarContent(stream, AvatarImageProcessor.OutputContentType);
    }

    public async Task<bool> RemoveForCommentAuthorAsync(Guid adminUserId, Guid commentId, CancellationToken cancellationToken)
    {
        var authorUserId = await dbContext.BlogComments
            .Where(c => c.Id == commentId)
            .Select(c => (Guid?)c.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (authorUserId is null)
        {
            return false;
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == authorUserId, cancellationToken);
        string? objectName = null;
        if (user is not null)
        {
            objectName = Clear(user);
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
        }

        // The photo was what these reports were about, on this comment and on every other comment
        // it was shown next to: all of them have had their action.
        var now = DateTimeOffset.UtcNow;
        var photoReports = await dbContext.BlogCommentReports
            .Where(r => r.Reason == BlogCommentReportReason.ProfilePhoto && r.Status == BlogCommentReportStatus.Open
                && dbContext.BlogComments.Any(c => c.Id == r.CommentId && c.UserId == authorUserId))
            .ToListAsync(cancellationToken);
        foreach (var report in photoReports)
        {
            report.Resolve(BlogCommentReportStatus.ActionTaken, adminUserId, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (objectName is not null)
        {
            await TryDeleteObjectAsync(objectName);
        }

        logger.LogInformation("Admin {AdminUserId} removed the profile photo of comment {CommentId}'s author.", adminUserId, commentId);
        return true;
    }

    public Task DeleteStoredObjectAsync(string objectName, CancellationToken cancellationToken) =>
        TryDeleteObjectAsync(objectName);

    /// <summary>Takes the photo off the row and returns the object that held it.</summary>
    private static string? Clear(ApplicationUser user)
    {
        var objectName = user.AvatarObjectName;
        user.AvatarObjectName = null;
        user.AvatarPublicId = null;
        user.AvatarUpdatedAt = null;
        user.ShowAvatarInComments = false;
        return objectName;
    }

    /// <summary>
    /// Saves the photo columns under Identity's concurrency stamp. Two uploads racing from two tabs
    /// would otherwise both succeed, the later row write would win, and the earlier upload's object
    /// would be left in the bucket with nothing pointing at it. False: someone else changed the row
    /// first, and this change was not written.
    /// </summary>
    private async Task<bool> TrySaveAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    private async Task<UserProfileResponse?> CurrentProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null ? null : UserProfiles.From(user);
    }

    // Deliberately not the request's token: once the row no longer points at the object, a client
    // that disconnects must not leave the bytes behind.
    private async Task TryDeleteObjectAsync(string objectName)
    {
        try
        {
            await storage.DeleteAsync(objectName, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to delete stored avatar object {ObjectName}.", objectName);
        }
    }

    private string MessageFor(AvatarImageProblem problem, long maxFileSizeBytes) => problem switch
    {
        AvatarImageProblem.Empty => localizer["AVATAR_EMPTY"],
        AvatarImageProblem.TooLarge => localizer["AVATAR_TOO_LARGE", maxFileSizeBytes / (1024 * 1024)],
        AvatarImageProblem.Unsupported => localizer["AVATAR_UNSUPPORTED"],
        AvatarImageProblem.TooManyPixels => localizer["AVATAR_TOO_MANY_PIXELS"],
        _ => throw new ArgumentOutOfRangeException(nameof(problem), problem, null)
    };
}
