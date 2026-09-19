using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Domain.Blog;

namespace AfterApply.Application.Blog;

/// <summary>
/// The admin side. Every method takes the calling admin's id and applies the visibility rule
/// from <see cref="BlogPost.IsEditableBy"/>: a post that is not published and not the caller's
/// is answered as null → 404, never 403, so one admin cannot enumerate another's drafts.
/// The caller has already passed <c>IAdminAccessService</c>.
/// </summary>
public interface IBlogAdminService
{
    Task<PagedResult<AdminBlogPostListItemResponse>> ListAsync(Guid adminUserId, AdminBlogListQuery query,
        CancellationToken cancellationToken);

    Task<AdminBlogPostResponse> CreateAsync(Guid adminUserId, CreateBlogPostRequest request, CancellationToken cancellationToken);

    Task<AdminBlogPostResponse?> GetAsync(Guid adminUserId, Guid postId, CancellationToken cancellationToken);

    /// <summary>The autosave.</summary>
    /// <exception cref="BlogPostRevisionConflictException">The request carries a stale revision.</exception>
    /// <exception cref="BlogPostFieldLockedException">Slug or language changed after publish.</exception>
    /// <exception cref="BlogTranslationInvalidException">The translation link points at the post
    /// itself, a post in the same language, or one the caller cannot see.</exception>
    Task<BlogDraftSavedResponse?> SaveDraftAsync(Guid adminUserId, Guid postId, SaveBlogDraftRequest request,
        CancellationToken cancellationToken);

    /// <summary>First publish and "update the live version" alike.</summary>
    /// <exception cref="BlogPostIncompleteException">No title or no body.</exception>
    /// <exception cref="BlogSlugTakenException">The hand-typed slug is another post's.</exception>
    Task<AdminBlogPostResponse?> PublishAsync(Guid adminUserId, Guid postId, CancellationToken cancellationToken);

    /// <exception cref="BlogPostNotPublishedException">The post is not on the site.</exception>
    Task<AdminBlogPostResponse?> UnpublishAsync(Guid adminUserId, Guid postId, CancellationToken cancellationToken);

    /// <summary>Removes the post, its likes, its media rows and — after the commit — its stored
    /// images. False when there is no such post the caller may see.</summary>
    Task<bool> DeleteAsync(Guid adminUserId, Guid postId, CancellationToken cancellationToken);
}

/// <summary>The anonymous side. Every query starts from <c>Status == Published</c> — the filter
/// is in the query, not in a mapper that could be bypassed.</summary>
public interface IBlogPublicService
{
    Task<PagedResult<BlogPostListItemResponse>> ListAsync(PublicBlogListQuery query, CancellationToken cancellationToken);

    /// <param name="viewerUserId">The signed-in reader, if the request happened to carry a valid
    /// token — only used to fill <c>LikedByMe</c>. Null for everyone else.</param>
    Task<BlogPostPublicResponse?> GetBySlugAsync(string language, string slug, Guid? viewerUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BlogSlugResponse>> ListSlugsAsync(CancellationToken cancellationToken);

    /// <summary>Whether the site has any published post at all — what decides if the web app
    /// shows a "Blog" link. Cached, and never throws: a database hiccup answers false rather than
    /// taking <c>/api/config</c> down with it.</summary>
    Task<bool> HasPublishedPostsAsync(CancellationToken cancellationToken);

    /// <summary>Like on, then off. Null when the post is not published — an unpublished post is
    /// not on any page, so "not found" is what the caller can see.</summary>
    Task<BlogLikeToggleResponse?> ToggleLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken);
}

public interface IBlogMediaService
{
    /// <summary>
    /// Validates and stores an image for a post the caller may edit. <paramref name="content"/>
    /// must be seekable — the format check reads the first bytes and then rewinds — which an
    /// <c>IFormFile</c> stream is. Null when there is no such post the caller may see.
    /// </summary>
    /// <exception cref="BlogUploadValidationException">Empty, too large, or not a PNG/JPEG/GIF/WebP
    /// by its own bytes.</exception>
    Task<BlogMediaResponse?> UploadAsync(Guid adminUserId, Guid postId, Stream content, long declaredLength,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens an image for reading. Public when its post is published; while the post is not, only
    /// the post's author gets it — anyone else, including other admins, gets null. Null also when
    /// the row or the object is missing: from the caller's side the file is simply not there.
    /// </summary>
    Task<BlogMediaContent?> OpenAsync(Guid mediaId, Guid? viewerUserId, CancellationToken cancellationToken);
}

/// <summary>
/// Turns the editor's HTML into the HTML the public page renders: an allowlist of tags,
/// attributes, CSS properties and URL schemes, with every image source folded to
/// <see cref="BlogMediaPath"/> or dropped. Runs on every draft save, so what is stored is already
/// what will be shown — the web app renders it as-is and never sanitizes on its own.
/// </summary>
public interface IBlogHtmlSanitizer
{
    string Sanitize(string html);
}

/// <summary>
/// Blob storage for blog images — the same three operations as <c>IFileStorage</c>, on a
/// different bucket. Its own interface rather than a parameter on the CV one so the two bindings
/// cannot be confused: a CV must never land where a public image is served from, and a CV bucket
/// must never be read by the anonymous media route.
/// </summary>
public interface IBlogMediaStorage
{
    Task SaveAsync(string objectName, Stream content, string contentType, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string objectName, CancellationToken cancellationToken);

    Task DeleteAsync(string objectName, CancellationToken cancellationToken);
}

/// <summary>An upload the server refused, carrying an already-localized reason. The endpoint
/// turns it into a 400 ValidationProblem keyed on the <c>file</c> field, the shape the web app's
/// error extraction already understands (see <c>CvUploadValidationException</c>).</summary>
public sealed class BlogUploadValidationException(IReadOnlyList<string> errors)
    : Exception("Blog image upload validation failed.")
{
    public IReadOnlyList<string> Errors { get; } = errors;

    public string Field => "file";
}
