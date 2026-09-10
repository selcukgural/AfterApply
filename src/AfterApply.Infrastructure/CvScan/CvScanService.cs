using AfterApply.Application.CvScan;
using AfterApply.Application.CvScan.Contracts;
using AfterApply.Application.Documents;
using AfterApply.Application.Localization;
using AfterApply.Domain.CvScan;
using AfterApply.Domain.Documents;
using AfterApply.Infrastructure.Documents;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
    ICvReviewProvider reviewProvider,
    IOptions<CvScanOptions> options,
    IOptions<StorageOptions> storageOptions,
    IStringLocalizer<SharedStrings> localizer,
    ILogger<CvScanService> logger)
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

        // Layer B runs after the score exists and can only ever add to the response. Whatever
        // happens here — the model refuses, the ceiling is reached, Vertex is down — the number
        // above is already final.
        var (reviewStatus, notes) = await ReviewAsync(request, extracted, cancellationToken);

        // Anonymous, and the only thing that survives the request. Saved before the response is
        // built so a scan that reached a score is counted as one — the stopping condition in
        // DEVELOPMENT_PLAN.md counts completed scans, and a scan the reader saw is completed.
        dbContext.CvScanResults.Add(CvScanResult.Create(score.Score, format,
            request.ContentNotesRequested, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);

        var preview = extracted.Text.Length > options.Value.PreviewCharacters
            ? extracted.Text[..options.Value.PreviewCharacters]
            : extracted.Text;

        return new CvScanResponse(
            score.Score, score.Categories, score.Findings,
            new CvScanDocumentSummary(format, extracted.PageCount, extracted.WordCount),
            preview,
            extracted.TextTruncated || preview.Length < extracted.Text.Length,
            reviewStatus, notes);
    }

    /// <summary>
    /// The content notes, and every reason there might not be any. Four outcomes, none of which is
    /// an error the caller has to handle: the feature is off, it was not asked for, it could not be
    /// done, or it worked.
    ///
    /// <b>Nothing in here can change the score</b>, which is why it runs after scoring and why its
    /// failure path is a status rather than an exception.
    /// </summary>
    private async Task<(CvReviewStatus Status, IReadOnlyList<CvContentNote> Notes)> ReviewAsync(
        CvScanRequest request, ExtractedCv extracted, CancellationToken cancellationToken)
    {
        if (!options.Value.LlmEnabled)
        {
            return (CvReviewStatus.Disabled, []);
        }

        if (!request.ContentNotesRequested)
        {
            return (CvReviewStatus.NotRequested, []);
        }

        // The day's ceiling, counted from the rows this feature writes rather than from a provider
        // dashboard nobody is watching. Reaching it degrades layer B and leaves layer A alone,
        // which is the behaviour DEVELOPMENT_PLAN.md asks for.
        // Built as an explicit UTC offset. DateTimeOffset.UtcNow.Date returns a DateTime with an
        // unspecified kind, and letting that convert implicitly would silently mean "midnight in
        // whatever timezone this process happens to run in" — a three-hour shift on a developer's
        // machine and a different day's ceiling than the one the row was counted into.
        var since = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        var todaysRequests = await dbContext.CvScanResults
            .CountAsync(result => result.ContentNotesRequested && result.ScannedAt >= since, cancellationToken);

        if (todaysRequests >= options.Value.Review.DailyRequestCeiling)
        {
            logger.LogWarning("CV scan content notes skipped: the daily ceiling of {Ceiling} was reached.",
                options.Value.Review.DailyRequestCeiling);
            return (CvReviewStatus.Unavailable, []);
        }

        try
        {
            var notes = await reviewProvider.ReviewAsync(
                new CvReviewRequest(extracted.Text, request.Locale), cancellationToken);

            return (CvReviewStatus.Ready, CvReviewNotes.Sanitize(notes, extracted.Text));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The message only — never the request, which is the caller's CV. A scan that lost its
            // notes is still a scan that answered the question the page asked.
            logger.LogWarning("CV scan content notes unavailable: {Reason}", exception.Message);
            return (CvReviewStatus.Unavailable, []);
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
