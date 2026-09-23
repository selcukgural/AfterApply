using AfterApply.Domain.Benchmark;
using AfterApply.Domain.Common;

namespace AfterApply.Domain.SilenceReports;

/// <summary>
/// One anonymous "I heard nothing back" from a company page: which stage the silence followed,
/// how long it has lasted, and whether a reply date had been promised (growth research
/// 2026-09-21, item 1.6 — the structured form of the hand-kept forum lists of companies that
/// never reply).
///
/// <b>No UserId and no IP address</b>, for the same reason as <see cref="BenchmarkSubmission"/>:
/// the form exists to be answerable without an account, and attaching a person to a complaint
/// about a named company is the one thing it must not do. The request that brought it in leaves
/// its own <c>RequestAudits</c> row (IP, path, time), never linked to this one. What that costs —
/// one person can report twice — is bounded by the per-IP rate limit, a thirty-day repeat block
/// per company kept only in Redis, and a publication floor of several reports across more than
/// one quarter.
///
/// <b>Never mixed into a response rate.</b> Only people who heard nothing fill this in, so it has
/// no denominator: it can say "N people reported silence", never "N % of candidates". It stays a
/// count of its own beside the tracker's figures.
///
/// Kept indefinitely (DECISIONS.md 2026-09-23): nothing in the row is personal data, and the
/// display reads only the last twelve months anyway.
/// </summary>
public sealed class SilenceReport : Entity
{
    public Guid CompanyId { get; private set; }

    public SilenceStage Stage { get; private set; }

    public SilenceWait Wait { get; private set; }

    /// <summary>Whether a reply date had been given and passed. Null = not said.</summary>
    public bool? PromiseGiven { get; private set; }

    /// <summary>The month the silence began — the submission month minus the wait band's lower
    /// bound, first of the month. Month precision on purpose: enough to place a report in a
    /// quarter, too coarse to match it to one interview.</summary>
    public DateOnly SilentSinceMonth { get; private set; }

    public string Locale { get; private set; } = string.Empty;

    /// <summary>The campaign channel from the link's <c>utm_source</c>, on the benchmark's list;
    /// null otherwise.</summary>
    public BenchmarkSource? Source { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    private SilenceReport()
    {
    }

    public static SilenceReport Create(Guid companyId, SilenceStage stage, SilenceWait wait, bool? promiseGiven,
        string locale, BenchmarkSource? source, DateTimeOffset submittedAt)
    {
        return new SilenceReport
        {
            CompanyId = companyId,
            Stage = stage,
            Wait = wait,
            PromiseGiven = promiseGiven,
            SilentSinceMonth = SilentSinceMonthFor(submittedAt, wait),
            Locale = locale,
            Source = source,
            SubmittedAt = submittedAt
        };
    }

    /// <summary>The first of the month the silence began: the submission date minus the band's
    /// lower bound (two weeks, one, two or three months).</summary>
    public static DateOnly SilentSinceMonthFor(DateTimeOffset submittedAt, SilenceWait wait)
    {
        var date = DateOnly.FromDateTime(submittedAt.UtcDateTime);
        var start = wait switch
        {
            SilenceWait.TwoToFourWeeks => date.AddDays(-14),
            SilenceWait.OneToTwoMonths => date.AddMonths(-1),
            SilenceWait.TwoToThreeMonths => date.AddMonths(-2),
            SilenceWait.OverThreeMonths => date.AddMonths(-3),
            _ => throw new ArgumentOutOfRangeException(nameof(wait), wait, null)
        };
        return new DateOnly(start.Year, start.Month, 1);
    }
}

/// <summary>The last step before the silence. Order = form order.</summary>
public enum SilenceStage
{
    AfterApplication,
    AfterHrScreen,
    AfterTechnicalInterview,
    AfterFinalInterview,
    AfterOfferTalk
}

/// <summary>How long the silence has lasted. Nothing under two weeks: that much waiting is still
/// ordinary, and counting it would turn a slow week into a complaint.</summary>
public enum SilenceWait
{
    TwoToFourWeeks,
    OneToTwoMonths,
    TwoToThreeMonths,
    OverThreeMonths
}
