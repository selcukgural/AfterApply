using AfterApply.Application.CvScan;
using AfterApply.Application.CvScan.Contracts;
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
    int UsedByApplicationCount,
    // The ATS-readability score of this file, when it has been scanned. Null until the user asks
    // for a scan — the upload itself never runs one, so uploading a CV keeps meaning exactly what
    // it meant before (2026-09-18).
    CvDocumentScanSummary? Scan);

/// <summary>What the CV list shows: the number and when it was measured. The report itself is
/// <see cref="CvDocumentScanReport"/>, one request away.</summary>
public sealed record CvDocumentScanSummary(int Score, DateTimeOffset ScannedAt);

/// <summary>
/// The stored report of one CV: the public scan's response minus the anonymous surface's extras
/// (no content notes — the stored scan is layer A only). Read back exactly as it was written, so
/// re-opening a report never re-reads the file.
/// </summary>
public sealed record CvDocumentScanReport(
    int Score,
    IReadOnlyList<CvScanCategoryScore> Categories,
    IReadOnlyList<CvScanFinding> Findings,
    CvScanDocumentSummary Document,
    string ExtractedTextPreview,
    bool ExtractedTextTruncated,
    DateTimeOffset ScannedAt);

/// <summary>The list plus the cap it is measured against. The cap ships in the payload rather than
/// being duplicated as a constant in the web app — the quota reading, the disabled upload button
/// and the server's own rule then cannot disagree.</summary>
public sealed record CvDocumentListResponse(IReadOnlyCollection<CvDocumentResponse> Items, int MaxCount);

/// <summary>A CV's bytes, ready to stream. The caller owns <see cref="Content"/> and must dispose
/// it. <see cref="ContentType"/> comes from the stored <see cref="CvFileFormat"/>, never from
/// anything the uploader claimed.</summary>
public sealed record CvDocumentContent(Stream Content, string FileName, string ContentType);
