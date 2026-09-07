using System.Globalization;
using AfterApply.Domain.Common;

namespace AfterApply.Domain.Documents;

/// <summary>
/// One CV file a user has uploaded. The bytes live in object storage; this row is the only index
/// into them — there is no listing of the bucket anywhere, so a row that disappears makes its
/// object unreachable by design.
/// </summary>
public sealed class CvDocument : AuditableEntity
{
    /// <summary>How many CVs one user may keep. A product cap, not a technical one, so it lives in
    /// the domain rather than in configuration: the number is quoted in the UI, in the help centre
    /// and in the privacy text, and all four have to agree.</summary>
    public const int MaxPerUser = 10;

    public Guid UserId { get; private set; }

    /// <summary>The name shown to the user, sanitized at upload. Never used to build a storage key
    /// or a filesystem path — <see cref="StorageObjectName"/> is, and it contains nothing the user
    /// supplied.</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>The object's key in the bucket: <c>cvs/{userId}/{id}{extension}</c>. Derived
    /// entirely from ids we generated, so no user input can traverse out of the user's own prefix.
    /// Persisted rather than recomputed so a later change to the layout cannot orphan what is
    /// already stored.</summary>
    public string StorageObjectName { get; private set; } = string.Empty;

    public CvFileFormat Format { get; private set; }

    public long SizeBytes { get; private set; }

    /// <summary>Pre-selected when the user creates an application. At most one per user — enforced
    /// by <see cref="Persistence"/>'s filtered unique index as well as by the service, because a
    /// concurrent pair of "make this the default" calls would otherwise leave two.</summary>
    public bool IsDefault { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>
    /// When the user gave explicit consent (KVKK m.6) for this particular upload. Stamped by
    /// <see cref="Create"/>, which is only reachable once the service has verified the consent
    /// flag — so a row existing is the record that consent was given for it.
    ///
    /// Nullable only for rows written before consent was required (2026-09-07). Backfilling those
    /// with a timestamp would be inventing a consent that was never given, which is the one thing
    /// a consent record must never do; null honestly means "we did not ask".
    /// </summary>
    public DateTimeOffset? ConsentAcceptedAt { get; private set; }

    private CvDocument()
    {
    }

    /// <remarks>Callers must have verified the user's explicit consent before calling this —
    /// <paramref name="now"/> is recorded as the moment it was given.</remarks>
    public static CvDocument Create(Guid userId, string fileName, CvFileFormat format, long sizeBytes,
        bool isDefault, DateTimeOffset now)
    {
        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), sizeBytes, "A stored CV always has bytes.");
        }

        var document = new CvDocument
        {
            UserId = userId,
            FileName = fileName,
            Format = format,
            SizeBytes = sizeBytes,
            IsDefault = isDefault,
            UploadedAt = now,
            ConsentAcceptedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        document.StorageObjectName = BuildStorageObjectName(userId, document.Id, format);
        return document;
    }

    /// <summary>The bucket key for a document. Public so tests can assert the shape without
    /// reaching through a stored row.</summary>
    public static string BuildStorageObjectName(Guid userId, Guid documentId, CvFileFormat format) =>
        string.Create(CultureInfo.InvariantCulture, $"cvs/{userId:D}/{documentId:D}{format.Extension()}");

    public void MarkAsDefault(DateTimeOffset now)
    {
        if (IsDefault)
        {
            return;
        }

        IsDefault = true;
        Touch(now);
    }

    public void ClearDefault(DateTimeOffset now)
    {
        if (!IsDefault)
        {
            return;
        }

        IsDefault = false;
        Touch(now);
    }
}
