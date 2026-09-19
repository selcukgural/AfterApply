using AfterApply.Application.Blog;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.Blog;
using AfterApply.Infrastructure.Documents;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Blog;

/// <summary>
/// Images in, images out. In: the same discipline as a CV upload (<c>CvDocumentService</c>) —
/// bytes decide the format, storage is written before the row, a failed row deletes the object.
/// Out: the post's status decides who may read — public once published, the author alone until
/// then — because an image is part of the draft it was uploaded into.
/// </summary>
internal sealed class BlogMediaService(
    AppDbContext dbContext,
    IBlogMediaStorage storage,
    IOptions<StorageOptions> options,
    IStringLocalizer<SharedStrings> localizer,
    ILogger<BlogMediaService> logger) : IBlogMediaService
{
    public async Task<BlogMediaResponse?> UploadAsync(Guid adminUserId, Guid postId, Stream content, long declaredLength,
        CancellationToken cancellationToken)
    {
        // The same visibility rule as every admin read (BlogAdminService.Visible): a post another
        // admin has not published is not there.
        var post = await dbContext.BlogPosts
            .AsNoTracking()
            .Where(p => p.Id == postId && (p.Status == BlogPostStatus.Published || p.AuthorUserId == adminUserId))
            .Select(p => new { p.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (post is null)
        {
            return null;
        }

        var maxFileSizeBytes = options.Value.MaxFileSizeBytes;
        if (BlogImageRules.InspectClaim(declaredLength, maxFileSizeBytes) is { } problem)
        {
            throw new BlogUploadValidationException([MessageFor(problem, maxFileSizeBytes)]);
        }

        var header = new byte[BlogImageRules.HeaderLengthBytes];
        var headerLength = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var headerSpan = header.AsSpan(0, headerLength);

        var format = BlogImageRules.DetectFormat(headerSpan)
                     ?? throw new BlogUploadValidationException([MessageFor(BlogImageProblem.Unsupported, maxFileSizeBytes)]);
        var dimensions = BlogImageRules.ReadDimensions(headerSpan, format);

        // The header read consumed bytes the upload still needs; IFormFile's stream is seekable.
        content.Seek(0, SeekOrigin.Begin);

        var media = BlogMedia.Create(postId, adminUserId, format.ContentType(), format.Extension(), declaredLength,
            dimensions?.Width, dimensions?.Height, DateTimeOffset.UtcNow);

        // Storage before the row, on purpose — see CvDocumentService.UploadAsync.
        await storage.SaveAsync(media.ObjectName, content, media.ContentType, cancellationToken);

        try
        {
            dbContext.BlogMedia.Add(media);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteObjectAsync(media.ObjectName, CancellationToken.None);
            throw;
        }

        return new BlogMediaResponse(media.Id, BlogMediaPath.For(media.Id), media.ContentType, media.ByteSize,
            media.Width, media.Height);
    }

    public async Task<BlogMediaContent?> OpenAsync(Guid mediaId, Guid? viewerUserId, CancellationToken cancellationToken)
    {
        var media = await dbContext.BlogMedia
            .AsNoTracking()
            .Where(m => m.Id == mediaId)
            .Join(dbContext.BlogPosts, m => m.PostId, p => p.Id,
                (m, p) => new { m.ObjectName, m.ContentType, p.Status, p.AuthorUserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (media is null)
        {
            return null;
        }

        var isPublic = media.Status == BlogPostStatus.Published;
        if (!isPublic && (viewerUserId is null || media.AuthorUserId != viewerUserId))
        {
            // Another admin's draft image is as absent as the draft itself.
            return null;
        }

        var stream = await storage.OpenReadAsync(media.ObjectName, cancellationToken);
        if (stream is null)
        {
            logger.LogWarning("Blog media {MediaId} has no stored object at {ObjectName}.", mediaId, media.ObjectName);
            return null;
        }

        return new BlogMediaContent(stream, media.ContentType, isPublic);
    }

    private async Task TryDeleteObjectAsync(string objectName, CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteAsync(objectName, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to delete stored blog media object {ObjectName}.", objectName);
        }
    }

    private string MessageFor(BlogImageProblem problem, long maxFileSizeBytes) => problem switch
    {
        BlogImageProblem.Empty => localizer["BLOG_IMAGE_EMPTY"],
        BlogImageProblem.TooLarge => localizer["BLOG_IMAGE_TOO_LARGE", maxFileSizeBytes],
        BlogImageProblem.Unsupported => localizer["BLOG_IMAGE_UNSUPPORTED"],
        _ => throw new ArgumentOutOfRangeException(nameof(problem), problem, null)
    };
}
