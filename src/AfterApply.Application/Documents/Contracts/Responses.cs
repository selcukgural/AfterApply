using AfterApply.Domain.Documents;

namespace AfterApply.Application.Documents.Contracts;

public sealed record CvDocumentResponse(
    Guid Id,
    string FileName,
    CvFileFormat Format,
    long SizeBytes,
    bool IsDefault,
    DateTimeOffset UploadedAt,
    // How many of this user's applications point at this CV. Read at list time rather than kept as
    // a counter on the row: it is only ever shown, never decided on, and a counter would be one
    // more thing to keep correct on every application delete.
    int UsedByApplicationCount);

/// <summary>The list plus the cap it is measured against. The cap ships in the payload rather than
/// being duplicated as a constant in the web app — the quota reading, the disabled upload button
/// and the server's own rule then cannot disagree.</summary>
public sealed record CvDocumentListResponse(IReadOnlyCollection<CvDocumentResponse> Items, int MaxCount);

/// <summary>A CV's bytes, ready to stream. The caller owns <see cref="Content"/> and must dispose
/// it. <see cref="ContentType"/> comes from the stored <see cref="CvFileFormat"/>, never from
/// anything the uploader claimed.</summary>
public sealed record CvDocumentContent(Stream Content, string FileName, string ContentType);
