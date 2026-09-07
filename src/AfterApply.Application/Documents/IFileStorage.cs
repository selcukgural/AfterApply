namespace AfterApply.Application.Documents;

/// <summary>
/// Blob storage, addressed by an opaque object name. Deliberately the smallest surface that serves
/// the CV feature — no listing, no signed URLs, no metadata: every download is proxied through the
/// API so that an object is only ever reachable by a caller the API has already authenticated and
/// found to own the row (see CvDocumentEndpoints).
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string objectName, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Opens the object for reading, or returns null when it is not there. Null is a real
    /// outcome, not an error: a row can outlive its object if a previous delete half-failed.</summary>
    Task<Stream?> OpenReadAsync(string objectName, CancellationToken cancellationToken);

    /// <summary>Removes the object. Succeeds when it was already gone — deletion is retried from
    /// background cleanup, so it has to be safe to run twice.</summary>
    Task DeleteAsync(string objectName, CancellationToken cancellationToken);
}
