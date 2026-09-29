using AfterApply.Application.Identity.Contracts;

namespace AfterApply.Application.Identity;

/// <summary>
/// Profile photos (DECISIONS.md 2026-09-28). One photo per account, stored only as the 256 px
/// WebP the server re-encodes it to; the upload's own bytes are never kept. The photo is reached
/// by a random public id that is not the user's id and is replaced on every change, so a URL seen
/// on a blog comment leads nowhere else and stops working once the photo or the permission to show
/// it is gone.
/// </summary>
public interface IAvatarService
{
    /// <summary>Replaces the caller's photo. Null when the account is gone.</summary>
    /// <exception cref="AvatarUploadValidationException">Empty, too large, not JPEG/PNG/WebP, too many pixels, or not an image at all.</exception>
    Task<UserProfileResponse?> UploadAsync(Guid userId, Stream content, long declaredLength, CancellationToken cancellationToken);

    /// <summary>Removes the caller's photo and turns "show on my comments" off. Null when the account is gone.</summary>
    Task<UserProfileResponse?> DeleteAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Turns "show on my blog comments" on or off. Turning it off also gives the photo a new
    /// public id, so a URL a reader already saw stops resolving. Null when the account is gone.</summary>
    Task<UserProfileResponse?> SetShowInCommentsAsync(Guid userId, bool showInComments, CancellationToken cancellationToken);

    /// <summary>The photo behind a public id, or null when no account holds that id now.</summary>
    Task<AvatarContent?> OpenAsync(Guid publicId, CancellationToken cancellationToken);

    /// <summary>The moderator's removal from a blog comment: the comment's author loses their photo
    /// and the "show" setting, and the comment's open profile-photo reports are closed as acted on.
    /// False when the comment does not exist.</summary>
    Task<bool> RemoveForCommentAuthorAsync(Guid adminUserId, Guid commentId, CancellationToken cancellationToken);

    /// <summary>Deletes stored objects after the rows that pointed at them are gone (account deletion).
    /// Never throws for a missing object; failures are logged, not raised.</summary>
    Task DeleteStoredObjectAsync(string objectName, CancellationToken cancellationToken);
}

/// <summary>A storage binding of its own (bucket or directory), never shared with the CVs or the
/// blog images — see <c>DependencyInjection.AddAvatars</c>.</summary>
public interface IAvatarStorage
{
    Task SaveAsync(string objectName, Stream content, string contentType, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string objectName, CancellationToken cancellationToken);

    Task DeleteAsync(string objectName, CancellationToken cancellationToken);
}

public sealed record AvatarContent(Stream Content, string ContentType);

/// <summary>An upload the server refused, with an already-localized reason, answered as a 400
/// ValidationProblem on the <c>file</c> field (the <c>BlogUploadValidationException</c> shape).</summary>
public sealed class AvatarUploadValidationException(string error) : Exception("Avatar upload validation failed.")
{
    public string Error { get; } = error;

    public string Field => "file";
}

/// <summary>Where a photo is served. Relative on purpose: the web app proxies the path to the API
/// (next.config.ts), so the page's <c>img-src 'self'</c> covers it.</summary>
public static class AvatarPath
{
    public const string Prefix = "/api/avatars/";

    public static string For(Guid publicId) => $"{Prefix}{publicId:D}";

    public static string? For(Guid? publicId) => publicId is { } id ? For(id) : null;
}
