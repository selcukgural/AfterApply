using AfterApply.Application.CvScan;
using AfterApply.Application.CvScan.Contracts;
using AfterApply.Application.Documents;
using AfterApply.Application.Localization;
using AfterApply.Domain.CvScan;
using AfterApply.Domain.Documents;
using AfterApply.Infrastructure.Documents;
using AfterApply.Infrastructure.Persistence;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CvScan;

/// <summary>
/// One scan, start to finish: check the file is what it says it is, read it, score it, and keep
/// nothing but the number.
///
/// The order matters and is the same order the stored-CV upload uses — consent, then the claim
/// (extension and size), then the file's own leading bytes — because each step is cheaper than the
/// one after it and this endpoint has no account in front of it.
///
/// <b>Nothing is written to storage and no text is logged.</b> The only row that outlives the
/// request is <see cref="CvScanResult"/>: a score, a format and a timestamp, with no identifier.
/// </summary>
internal sealed class CvScanService(
    AppDbContext dbContext,
    ICvTextExtractor extractor,
    IOptions<CvScanOptions> options,
    IOptions<StorageOptions> storageOptions,
    IStringLocalizer<SharedStrings> localizer)
    : ICvScanService
{
    private const long MinimumFormMilliseconds = 1_500;

    public async Task<CvScanResponse> ScanAsync(CvScanRequest request, CancellationToken cancellationToken)
    {
        // A filled honeypot means something walked the form filling every input it found. Refused
        // with the same generic message a malformed request gets: telling a script which field
        // gave it away is the one thing that would make the field worthless.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            throw new CvUploadValidationException([localizer["CV_SCAN_REJECTED"]]);
        }

        // A form that was open for less than this was not filled in by a person: choosing a file
        // alone takes longer. Same class of defence as the honeypot, with the same limits — the
        // number comes from the client, so it catches the careless rather than the determined.
        if (request.ElapsedMilliseconds is null or < MinimumFormMilliseconds)
        {
            throw new CvUploadValidationException([localizer["CV_SCAN_REJECTED"]]);
        }

        if (!request.ConsentAccepted)
        {
            throw new CvUploadValidationException([localizer["CV_SCAN_CONSENT_REQUIRED"]],
                field: "consentAccepted");
        }

        var maxFileSizeBytes = storageOptions.Value.MaxFileSizeBytes;

        if (CvFileRules.InspectClaim(request.FileName, request.DeclaredLength, maxFileSizeBytes) is { } problem)
        {
            throw new CvUploadValidationException([MessageFor(problem, maxFileSizeBytes)]);
        }

        var format = CvFileRules.FormatFromFileName(request.FileName)!.Value;

        var header = new byte[CvFileRules.HeaderLengthBytes];
        var headerLength = await request.Content.ReadAtLeastAsync(header, header.Length,
            throwOnEndOfStream: false, cancellationToken);

        if (!CvFileRules.HeaderMatchesFormat(header.AsSpan(0, headerLength), format))
        {
            throw new CvUploadValidationException(
                [MessageFor(CvFileProblem.ContentDoesNotMatchExtension, maxFileSizeBytes)]);
        }

        request.Content.Seek(0, SeekOrigin.Begin);

        ExtractedCv extracted;
        try
        {
            extracted = await extractor.ExtractAsync(request.Content, format, cancellationToken);
        }
        catch (CvExtractionException exception)
        {
            throw new CvUploadValidationException([MessageFor(exception.Failure)]);
        }

        var score = CvScanScoring.Score(CvScanChecks.Run(extracted));

        // Anonymous, and the only thing that survives the request. Saved before the response is
        // built so a scan that reached a score is counted as one — the stopping condition in
        // DEVELOPMENT_PLAN.md counts completed scans, and a scan the reader saw is completed.
        dbContext.CvScanResults.Add(CvScanResult.Create(score.Score, format, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);

        var preview = extracted.Text.Length > options.Value.PreviewCharacters
            ? extracted.Text[..options.Value.PreviewCharacters]
            : extracted.Text;

        return new CvScanResponse(
            score.Score, score.Categories, score.Findings,
            new CvScanDocumentSummary(format, extracted.PageCount, extracted.WordCount),
            preview,
            extracted.TextTruncated || preview.Length < extracted.Text.Length);
    }

    private string MessageFor(CvFileProblem problem, long maxFileSizeBytes) => problem switch
    {
        CvFileProblem.UnsupportedExtension => localizer["CV_UNSUPPORTED_FILE_TYPE"],
        CvFileProblem.Empty => localizer["CV_FILE_EMPTY"],
        CvFileProblem.TooLarge => localizer["CV_FILE_TOO_LARGE", maxFileSizeBytes],
        CvFileProblem.ContentDoesNotMatchExtension => localizer["CV_FILE_CONTENT_MISMATCH"],
        _ => throw new ArgumentOutOfRangeException(nameof(problem), problem, null)
    };

    private string MessageFor(CvExtractionFailure failure) => failure switch
    {
        CvExtractionFailure.PasswordProtected => localizer["CV_SCAN_PASSWORD_PROTECTED"],
        CvExtractionFailure.Corrupt => localizer["CV_SCAN_UNREADABLE"],
        CvExtractionFailure.TooManyPages => localizer["CV_SCAN_TOO_MANY_PAGES", options.Value.MaxPages],
        CvExtractionFailure.TooExpensive => localizer["CV_SCAN_TOO_EXPENSIVE"],
        CvExtractionFailure.UnsupportedLegacyFormat => localizer["CV_SCAN_LEGACY_DOC"],
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
    };
}
