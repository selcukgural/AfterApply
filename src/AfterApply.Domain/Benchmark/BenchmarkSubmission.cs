using AfterApply.Domain.Common;

namespace AfterApply.Domain.Benchmark;

/// <summary>
/// One person's answer to "how many did you send, and how many ever came back".
///
/// This table exists because the aggregate question — <i>is my reply rate normal?</i> — is the one
/// thing no product on this market answers, and it does not have to wait for the tracker to gather
/// users first. Lattice's annual industry report, the closest working example, is a survey of about
/// a thousand people collected over two months, not product telemetry. Asking is allowed to come
/// before measuring.
///
/// <b>There is no UserId, and that is a deliberate departure from the plan's line about an optional
/// one.</b> A user id would make this a user-owned table (cascade-from-Users wiring, DECISIONS.md
/// 2026-09-07) and would attach a person to a self-reported figure, for the sake of a de-duplication
/// and a "your survey answer vs your tracked data" screen that nobody has asked for. Both are
/// reasons not to have it yet. What it costs is stated openly in the page's methodology note: one
/// person can answer twice. The defence is the median rather than the mean — a handful of extreme
/// or repeated answers moves a median very little — plus the rate limit.
///
/// Nothing here is personal data: four coarse categories and two counts, no identifier, no free
/// text, no IP address.
/// </summary>
public sealed class BenchmarkSubmission : Entity
{
    /// <summary>How many applications were sent in the period. Bounded by the validator — a
    /// four-digit answer is a typo or a joke, and either way it would drag an average around.</summary>
    public int ApplicationCount { get; private set; }

    /// <summary>How many drew any human reply. <b>Rejections count.</b> What is being measured is
    /// "did a person ever look at this", not "did I get the job" — the same definition the guide
    /// article and the spreadsheet template already use, and the form says so where it is
    /// asked.</summary>
    public int ReplyCount { get; private set; }

    public BenchmarkSector Sector { get; private set; }

    public BenchmarkPeriod Period { get; private set; }

    public BenchmarkSeniority? Seniority { get; private set; }

    public BenchmarkLocation? Location { get; private set; }

    /// <summary>The site language the answer was given in. Not a breakdown axis anyone asked for —
    /// it is here because a benchmark drawn overwhelmingly from one language is a fact about the
    /// sample that whoever publishes the number needs to know.</summary>
    public string Locale { get; private set; } = string.Empty;

    public DateTimeOffset SubmittedAt { get; private set; }

    private BenchmarkSubmission()
    {
    }

    public static BenchmarkSubmission Create(
        int applicationCount, int replyCount, BenchmarkSector sector, BenchmarkPeriod period,
        BenchmarkSeniority? seniority, BenchmarkLocation? location, string locale,
        DateTimeOffset submittedAt)
    {
        return new BenchmarkSubmission
        {
            ApplicationCount = applicationCount,
            ReplyCount = replyCount,
            Sector = sector,
            Period = period,
            Seniority = seniority,
            Location = location,
            Locale = locale,
            SubmittedAt = submittedAt
        };
    }

    /// <summary>Replies over applications. The single number the whole feature is about.</summary>
    public double ReplyRate => ApplicationCount == 0 ? 0 : (double)ReplyCount / ApplicationCount;
}
