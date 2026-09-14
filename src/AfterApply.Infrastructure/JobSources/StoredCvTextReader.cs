using AfterApply.Application.CvScan;
using AfterApply.Application.Documents;
using AfterApply.Application.JobSources;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// The user's default CV (or their newest, if none is marked), read from storage and put through
/// the same extractor the CV scan uses. The text lives for the length of the call and is never
/// written anywhere: the CV documents table stays a table of files, and the only copy of the
/// text is the one on its way to the model.
/// </summary>
internal sealed class StoredCvTextReader(
    AppDbContext dbContext,
    IFileStorage storage,
    ICvTextExtractor extractor,
    ILogger<StoredCvTextReader> logger) : IUserCvTextReader
{
    public async Task<string?> ReadDefaultCvTextAsync(Guid userId, CancellationToken cancellationToken)
    {
        var document = await dbContext.CvDocuments.AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.IsDefault)
            .ThenByDescending(d => d.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return null;
        }

        await using var stream = await storage.OpenReadAsync(document.StorageObjectName, cancellationToken);
        if (stream is null)
        {
            logger.LogWarning("CV document {DocumentId} has no object in storage; the user is not scored this week", document.Id);
            return null;
        }

        try
        {
            var extracted = await extractor.ExtractAsync(stream, document.Format, cancellationToken);
            return string.IsNullOrWhiteSpace(extracted.Text) ? null : extracted.Text;
        }
        catch (CvExtractionException exception)
        {
            logger.LogWarning("CV document {DocumentId} could not be read ({Failure}); the user is not scored this week",
                document.Id, exception.Failure);
            return null;
        }
    }
}
