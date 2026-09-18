using System.Text.Json;
using AfterApply.Application.CvScan;
using AfterApply.Application.CvScan.Contracts;
using AfterApply.Application.Documents;
using AfterApply.Application.Documents.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.Documents;
using AfterApply.Infrastructure.CvScan;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Documents;

internal sealed class CvDocumentService(
    AppDbContext dbContext,
    IFileStorage storage,
    ICvTextExtractor extractor,
    IOptions<StorageOptions> options,
    IOptions<CvScanOptions> scanOptions,
    IStringLocalizer<SharedStrings> localizer,
    ILogger<CvDocumentService> logger)
    : ICvDocumentService
{
    /// <summary>The stored report's wire shape, fixed here: the JSON in the row is read back by
    /// this class only, and the web app gets the same casing it gets from every other endpoint.</summary>
    private static readonly JsonSerializerOptions ReportJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CvDocumentListResponse> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await dbContext.CvDocuments
            .Where(d => d.UserId == userId)
            // The default first, then newest — the list is short and never paged, so ordering it
            // for reading beats ordering it for the database.
            .OrderByDescending(d => d.IsDefault)
            .ThenByDescending(d => d.UploadedAt)
            .Select(d => new CvDocumentResponse(
                d.Id, d.FileName, d.Format, d.SizeBytes, d.IsDefault, d.UploadedAt,
                dbContext.Applications.Count(a => a.CvDocumentId == d.Id),
                dbContext.CvDocumentScans
                    .Where(scan => scan.CvDocumentId == d.Id)
                    .Select(scan => new CvDocumentScanSummary(scan.Score, scan.ScannedAt))
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new CvDocumentListResponse(items, CvDocument.MaxPerUser);
    }

    public async Task<CvDocumentResponse> UploadAsync(Guid userId, Stream content, string fileName,
        long declaredLength, bool consentAccepted, CancellationToken cancellationToken)
    {
        // First, before anything is read or stored: without consent there is no lawful basis to
        // hold the file, so there is nothing to validate afterwards.
        if (!consentAccepted)
        {
            throw new CvUploadValidationException([localizer["CV_CONSENT_REQUIRED"]], field: "consentAccepted");
        }

        var maxFileSizeBytes = options.Value.MaxFileSizeBytes;

        if (CvFileRules.InspectClaim(fileName, declaredLength, maxFileSizeBytes) is { } problem)
        {
            throw new CvUploadValidationException([MessageFor(problem, maxFileSizeBytes)]);
        }

        // Non-null: InspectClaim already refused every name it cannot read a format from.
        var format = CvFileRules.FormatFromFileName(fileName)!.Value;

        var header = new byte[CvFileRules.HeaderLengthBytes];
        var headerLength = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false,
            cancellationToken);

        if (!CvFileRules.HeaderMatchesFormat(header.AsSpan(0, headerLength), format))
        {
            throw new CvUploadValidationException(
                [MessageFor(CvFileProblem.ContentDoesNotMatchExtension, maxFileSizeBytes)]);
        }

        // The header read consumed bytes the upload still needs. IFormFile's stream is seekable
        // (ASP.NET Core has already buffered the multipart section, which is also why
        // declaredLength above is a measured length rather than a client's claim), so rewinding is
        // cheaper and clearer than splicing the header back onto the front.
        content.Seek(0, SeekOrigin.Begin);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(userId, cancellationToken);

        var existingCount = await dbContext.CvDocuments.CountAsync(d => d.UserId == userId, cancellationToken);
        if (existingCount >= CvDocument.MaxPerUser)
        {
            throw new CvDocumentLimitReachedException();
        }

        // The first CV a user uploads becomes their default, so "default" is never an empty concept
        // for someone who has any CV at all — the same invariant DeleteAsync restores below.
        var document = CvDocument.Create(userId, CvFileRules.SanitizeFileName(fileName, format), format,
            declaredLength, isDefault: existingCount == 0, DateTimeOffset.UtcNow);

        // Storage before the row, on purpose. A failure here leaves nothing behind (a GCS upload
        // only becomes visible when it completes), whereas committing the row first would leave a
        // listed CV whose bytes never arrived — visible to the user and impossible to fix from the
        // UI. The reverse leak (object written, row not committed) is cleaned up below.
        await storage.SaveAsync(document.StorageObjectName, content, format.ContentType(), cancellationToken);

        try
        {
            dbContext.CvDocuments.Add(document);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteObjectAsync(document.StorageObjectName, CancellationToken.None);
            throw;
        }

        return new CvDocumentResponse(document.Id, document.FileName, document.Format, document.SizeBytes,
            document.IsDefault, document.UploadedAt, UsedByApplicationCount: 0, Scan: null);
    }

    public async Task<CvDocumentContent?> OpenAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await dbContext.CvDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);

        if (document is null)
        {
            return null;
        }

        var stream = await storage.OpenReadAsync(document.StorageObjectName, cancellationToken);
        if (stream is null)
        {
            // A row whose object is missing: only reachable if a delete half-failed. Logged rather
            // than thrown — from the caller's side the file is simply not there.
            logger.LogWarning("CV document {DocumentId} has no stored object at {ObjectName}.",
                document.Id, document.StorageObjectName);
            return null;
        }

        return new CvDocumentContent(stream, document.FileName, document.Format.ContentType());
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(userId, cancellationToken);

        var document = await dbContext.CvDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);

        if (document is null)
        {
            return false;
        }

        var objectName = document.StorageObjectName;
        var wasDefault = document.IsDefault;

        dbContext.CvDocuments.Remove(document);

        if (wasDefault)
        {
            // Keep "a user with any CV has a default": hand the badge to the newest survivor.
            var successor = await dbContext.CvDocuments
                .Where(d => d.UserId == userId && d.Id != documentId)
                .OrderByDescending(d => d.UploadedAt)
                .FirstOrDefaultAsync(cancellationToken);

            successor?.MarkAsDefault(DateTimeOffset.UtcNow);
        }

        // Applications pointing at this CV have their reference cleared by the FK's ON DELETE SET
        // NULL — see CvDocumentConfiguration. The application itself is untouched: the user deleted
        // a file, not a piece of their history.
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // After the commit, and best-effort: the row is what makes the object reachable, so a
        // failed object delete is a storage leak to clean up, not a failed request to show the
        // user. Nothing can read it any more either way.
        await TryDeleteObjectAsync(objectName, cancellationToken);

        return true;
    }

    public async Task<CvDocumentResponse?> SetDefaultAsync(Guid userId, Guid documentId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(userId, cancellationToken);

        var documents = await dbContext.CvDocuments
            .Where(d => d.UserId == userId && (d.Id == documentId || d.IsDefault))
            .ToListAsync(cancellationToken);

        var target = documents.FirstOrDefault(d => d.Id == documentId);
        if (target is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var other in documents.Where(d => d.Id != documentId))
        {
            other.ClearDefault(now);
        }

        target.MarkAsDefault(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var usedByApplicationCount = await dbContext.Applications
            .CountAsync(a => a.CvDocumentId == target.Id, cancellationToken);
        var scan = await dbContext.CvDocumentScans
            .Where(s => s.CvDocumentId == target.Id)
            .Select(s => new CvDocumentScanSummary(s.Score, s.ScannedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return new CvDocumentResponse(target.Id, target.FileName, target.Format, target.SizeBytes,
            target.IsDefault, target.UploadedAt, usedByApplicationCount, scan);
    }

    public async Task<CvDocumentScanReport?> ScanAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await dbContext.CvDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);

        if (document is null)
        {
            return null;
        }

        await using var stored = await storage.OpenReadAsync(document.StorageObjectName, cancellationToken);
        if (stored is null)
        {
            logger.LogWarning("CV document {DocumentId} has no stored object at {ObjectName}.",
                document.Id, document.StorageObjectName);
            return null;
        }

        // The extractor seeks (PdfPig reads the cross-reference table from the end), and a bucket
        // stream does not. The file is at most StorageOptions.MaxFileSizeBytes, so buffering it
        // whole is the honest option — and it is in memory for one request, like the public scan.
        using var content = new MemoryStream();
        await stored.CopyToAsync(content, cancellationToken);
        content.Seek(0, SeekOrigin.Begin);

        ExtractedCv extracted;
        try
        {
            extracted = await extractor.ExtractAsync(content, document.Format, cancellationToken);
        }
        catch (CvExtractionException exception)
        {
            throw new CvUploadValidationException([MessageFor(exception.Failure)]);
        }

        // The public scan's layer A, exactly: same checks, same weights, same rounding. Layer B (the
        // model's content notes) is deliberately not run here — this is a report about what a
        // parser reads, kept with the file, not a second consent surface.
        var score = CvScanScoring.Score(CvScanChecks.Run(extracted));
        var previewLength = scanOptions.Value.PreviewCharacters;
        var preview = extracted.Text.Length > previewLength ? extracted.Text[..previewLength] : extracted.Text;
        var now = DateTimeOffset.UtcNow;

        var report = new CvDocumentScanReport(
            score.Score, score.Categories, score.Findings,
            new CvScanDocumentSummary(document.Format, extracted.PageCount, extracted.WordCount),
            preview,
            extracted.TextTruncated || preview.Length < extracted.Text.Length,
            now);
        var reportJson = JsonSerializer.Serialize(report, ReportJsonOptions);

        var existing = await dbContext.CvDocumentScans
            .FirstOrDefaultAsync(s => s.CvDocumentId == document.Id, cancellationToken);
        if (existing is null)
        {
            dbContext.CvDocumentScans.Add(CvDocumentScan.Create(document.Id, score.Score, reportJson, now));
        }
        else
        {
            existing.Replace(score.Score, reportJson, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return report;
    }

    public async Task<CvDocumentScanReport?> GetScanAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        // Ownership through the document, never through the scan row alone.
        var reportJson = await dbContext.CvDocumentScans
            .AsNoTracking()
            .Where(s => s.CvDocumentId == documentId
                        && dbContext.CvDocuments.Any(d => d.Id == documentId && d.UserId == userId))
            .Select(s => s.ReportJson)
            .FirstOrDefaultAsync(cancellationToken);

        return reportJson is null ? null : JsonSerializer.Deserialize<CvDocumentScanReport>(reportJson, ReportJsonOptions);
    }

    public async Task DeleteStoredObjectsAsync(IReadOnlyCollection<string> storageObjectNames,
        CancellationToken cancellationToken)
    {
        foreach (var objectName in storageObjectNames)
        {
            await TryDeleteObjectAsync(objectName, cancellationToken);
        }
    }

    /// <summary>
    /// Serializes this user's CV writes against each other for the rest of the transaction.
    /// Needed because "count, then insert" is not atomic under READ COMMITTED: two uploads racing
    /// at the cap would both read nine and both write, leaving eleven. A unique index cannot
    /// express "at most ten rows", and the same lock is what keeps "exactly one default" true
    /// without one — see CvDocumentConfiguration for why that index is not unique either.
    /// Transaction-scoped, so it is released by the commit or rollback and never leaks.
    /// </summary>
    private Task LockUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({userId.ToString()}, 0))", cancellationToken);

    private async Task TryDeleteObjectAsync(string objectName, CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteAsync(objectName, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to delete stored CV object {ObjectName}.", objectName);
        }
    }

    private string MessageFor(CvFileProblem problem, long maxFileSizeBytes) => problem switch
    {
        CvFileProblem.UnsupportedExtension => localizer["CV_UNSUPPORTED_FILE_TYPE"],
        CvFileProblem.Empty => localizer["CV_FILE_EMPTY"],
        CvFileProblem.TooLarge => localizer["CV_FILE_TOO_LARGE", maxFileSizeBytes],
        CvFileProblem.ContentDoesNotMatchExtension => localizer["CV_FILE_CONTENT_MISMATCH"],
        _ => throw new ArgumentOutOfRangeException(nameof(problem), problem, null)
    };

    // The public scan's own wording for the same failures (CvScanService.MessageFor): a stored file
    // that cannot be read gets the sentence the reader would have seen on the public page.
    private string MessageFor(CvExtractionFailure failure) => failure switch
    {
        CvExtractionFailure.PasswordProtected => localizer["CV_SCAN_PASSWORD_PROTECTED"],
        CvExtractionFailure.Corrupt => localizer["CV_SCAN_UNREADABLE"],
        CvExtractionFailure.TooManyPages => localizer["CV_SCAN_TOO_MANY_PAGES", scanOptions.Value.MaxPages],
        CvExtractionFailure.TooExpensive => localizer["CV_SCAN_TOO_EXPENSIVE"],
        CvExtractionFailure.UnsupportedLegacyFormat => localizer["CV_SCAN_LEGACY_DOC"],
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
    };
}
