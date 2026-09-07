using AfterApply.Application.Common;
using AfterApply.Application.Documents.Contracts;
using AfterApply.Domain.Documents;

namespace AfterApply.Application.Documents;

public interface ICvDocumentService
{
    Task<CvDocumentListResponse> GetAllAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Validates and stores an uploaded CV. <paramref name="content"/> must be seekable — the
    /// format check reads the first bytes and then rewinds — which an <c>IFormFile</c> stream is,
    /// because ASP.NET Core has already buffered the multipart section. That buffering is also why
    /// <paramref name="declaredLength"/> is a measured length rather than a claim from the client.
    /// </summary>
    /// <exception cref="CvUploadValidationException">The file was refused — wrong extension, empty,
    /// too large, or its bytes are not the format its name claims.</exception>
    /// <exception cref="CvDocumentLimitReachedException">The user is already at the per-user
    /// cap.</exception>
    Task<CvDocumentResponse> UploadAsync(Guid userId, Stream content, string fileName, long declaredLength,
        CancellationToken cancellationToken);

    /// <summary>Opens a CV the given user owns, or returns null when there is no such row for
    /// them — a document belonging to someone else is "not found", never "forbidden".</summary>
    Task<CvDocumentContent?> OpenAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);

    Task<CvDocumentResponse?> SetDefaultAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every stored object for a user. Called from account deletion, after the rows are
    /// already gone — see AuthService.DeleteAccountAsync for why the two cannot be one transaction.
    /// </summary>
    Task DeleteStoredObjectsAsync(IReadOnlyCollection<string> storageObjectNames, CancellationToken cancellationToken);
}

/// <summary>An upload the server refused, carrying already-localized reasons. Mirrors
/// <c>CsvImportValidationException</c>: the endpoint turns it into a 400 ValidationProblem keyed on
/// the form field, which is what the web app's error extraction already understands.</summary>
public sealed class CvUploadValidationException(IReadOnlyList<string> errors)
    : Exception("CV upload validation failed.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>
/// The per-user CV cap, hit. A <c>CodedException</c> rather than a <c>DomainException</c> so the
/// limit itself travels with the error and the localized text can say the number — otherwise
/// <see cref="CvDocument.MaxPerUser"/> would have to be spelled out again in every resx and could
/// drift from the rule the server actually enforces.
/// </summary>
public sealed class CvDocumentLimitReachedException()
    : CodedException("CV_DOCUMENT_LIMIT_REACHED",
        $"A user may keep at most {CvDocument.MaxPerUser} CV documents.", CvDocument.MaxPerUser);
