namespace AfterApply.Domain.Applications;

/// <summary>
/// The one reading of a reply promise, shared by the application page, the reminder scan and the
/// response-rate aggregate so the three can never disagree on whether a company kept its word.
///
/// A promise belongs to a stage: it was given while the application sat in some status, entered
/// at <c>since</c>. The company's answer is the first move out of that stage into a status a
/// candidate would call a reply (<see cref="ApplicationStatusClassification.RespondedStatuses"/>)
/// — a Ghosted mark is the candidate giving up, not an answer, so it is read past. Kept means that
/// answer came by the promised date plus <see cref="GraceDays"/>; the grace absorbs a reply sent
/// late on the day, a weekend, a time zone, and the candidate logging it a day after.
/// </summary>
public static class ReplyPromises
{
    public const int GraceDays = 2;

    public static ReplyPromiseEvaluation Evaluate(DateOnly promisedBy, DateTimeOffset since,
        IEnumerable<(ApplicationStatus ToStatus, DateTimeOffset ChangedAt)> history, DateTimeOffset now)
    {
        var deadline = promisedBy.AddDays(GraceDays);

        foreach (var (toStatus, changedAt) in history.Where(h => h.ChangedAt > since).OrderBy(h => h.ChangedAt))
        {
            var day = DateOnly.FromDateTime(changedAt.UtcDateTime);

            if (ApplicationStatusClassification.RespondedStatuses.Contains(toStatus))
            {
                return day <= deadline
                    ? new ReplyPromiseEvaluation(ReplyPromiseOutcome.Kept, CountsAsKept: true, CountsAsBroken: false)
                    : new ReplyPromiseEvaluation(ReplyPromiseOutcome.Late, CountsAsKept: false, CountsAsBroken: true);
            }

            if (toStatus == ApplicationStatus.Withdrawn)
            {
                // Leaving before the deadline takes the question away; leaving after it does not
                // excuse the silence that came first.
                return day <= deadline
                    ? new ReplyPromiseEvaluation(ReplyPromiseOutcome.Void, CountsAsKept: false, CountsAsBroken: false)
                    : new ReplyPromiseEvaluation(ReplyPromiseOutcome.Overdue, CountsAsKept: false, CountsAsBroken: true);
            }
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (today <= promisedBy)
        {
            return new ReplyPromiseEvaluation(ReplyPromiseOutcome.Pending, CountsAsKept: false, CountsAsBroken: false);
        }

        // Overdue to the candidate from the day after the date; broken for the aggregate only once
        // the grace has run out too, so a reply still inside it can turn the row into Kept.
        return new ReplyPromiseEvaluation(ReplyPromiseOutcome.Overdue, CountsAsKept: false, CountsAsBroken: today > deadline);
    }
}

/// <param name="CountsAsKept">True only for <see cref="ReplyPromiseOutcome.Kept"/>.</param>
/// <param name="CountsAsBroken">Settled against the company: answered late, or still silent after
/// the grace. Pending, Void and an Overdue still inside the grace count as neither.</param>
public sealed record ReplyPromiseEvaluation(ReplyPromiseOutcome Outcome, bool CountsAsKept, bool CountsAsBroken);
