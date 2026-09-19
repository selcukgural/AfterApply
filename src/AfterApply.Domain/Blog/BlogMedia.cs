using System.Globalization;
using AfterApply.Domain.Common;

namespace AfterApply.Domain.Blog;

/// <summary>
/// One image uploaded into a post. The bytes live in the blog media bucket; this row is the only
/// index into them, and it is what decides who may read them: the post's status (public once the
/// post is published, the author alone while it is not) — see <c>BlogMediaService.OpenAsync</c>.
/// </summary>
public sealed class BlogMedia : Entity
{
    public Guid PostId { get; private set; }

    /// <summary>Who uploaded it. Nullable: the account may be deleted while the post lives on.</summary>
    public Guid? UploaderUserId { get; private set; }

    /// <summary>The object's key in the bucket: <c>blog/{postId}/{id}.{ext}</c>. Built from ids we
    /// generated and an extension we chose from the file's own bytes, so nothing the client sent
    /// reaches the key.</summary>
    public string ObjectName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long ByteSize { get; private set; }

    /// <summary>Pixel dimensions when the header made them cheap to read (PNG, GIF), else null.
    /// Informational — the editor decides display size.</summary>
    public int? Width { get; private set; }

    public int? Height { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private BlogMedia()
    {
    }

    public static BlogMedia Create(Guid postId, Guid uploaderUserId, string contentType, string extension,
        long byteSize, int? width, int? height, DateTimeOffset now)
    {
        if (byteSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteSize), byteSize, "A stored image always has bytes.");
        }

        var media = new BlogMedia
        {
            PostId = postId,
            UploaderUserId = uploaderUserId,
            ContentType = contentType,
            ByteSize = byteSize,
            Width = width,
            Height = height,
            CreatedAt = now
        };
        media.ObjectName = BuildObjectName(postId, media.Id, extension);
        return media;
    }

    /// <summary>Public so tests can assert the shape without reaching through a stored row.</summary>
    public static string BuildObjectName(Guid postId, Guid mediaId, string extension) =>
        string.Create(CultureInfo.InvariantCulture, $"blog/{postId:D}/{mediaId:D}{extension}");
}
