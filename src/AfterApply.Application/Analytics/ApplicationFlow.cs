using AfterApply.Application.Analytics.Contracts;
using AfterApply.Domain.Applications;

namespace AfterApply.Application.Analytics;

/// <summary>The window the shareable flow card covers, counted back from today by application date.</summary>
public enum FlowPeriod
{
    Last30Days,
    Last90Days,
    All
}

public static class FlowPeriods
{
    /// <summary>
    /// Reads the query-string spelling ("30", "90", "all"). Anything else is refused rather than
    /// defaulted, so a typo never quietly renders a card over the wrong window.
    /// </summary>
    public static bool TryParse(string? value, out FlowPeriod period)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "30":
                period = FlowPeriod.Last30Days;
                return true;
            case "90":
                period = FlowPeriod.Last90Days;
                return true;
            case "all":
                period = FlowPeriod.All;
                return true;
            default:
                period = default;
                return false;
        }
    }

    /// <summary>The earliest application date the period includes, or null for "all".</summary>
    public static DateTimeOffset? StartOf(FlowPeriod period, DateTimeOffset now) => period switch
    {
        FlowPeriod.Last30Days => now.AddDays(-30),
        FlowPeriod.Last90Days => now.AddDays(-90),
        _ => null
    };
}

/// <summary>One application as the flow card sees it: where it is now and every status it has passed through.</summary>
public sealed record ApplicationFlowItem(
    ApplicationStatus CurrentStatus,
    IReadOnlyCollection<ApplicationStatus> History,
    DateTimeOffset AppliedAt);

/// <summary>
/// Sorts a person's applications into the nodes of the shareable flow card (Sankey): every
/// application lands in exactly one first-column node, and every one that reached an interview
/// lands in exactly one second-column node — so both columns always add up, which is what the card
/// URL is validated against on the web side.
/// </summary>
public static class ApplicationFlowClassifier
{
    public static ApplicationFlowCounts Classify(
        IEnumerable<ApplicationFlowItem> applications,
        DateTimeOffset now,
        int unansweredAfterDays)
    {
        var counts = new MutableCounts();

        foreach (var application in applications)
        {
            counts.Total++;

            // The history table may not carry the status the application was created in, so the
            // current status is always part of what it has "passed through".
            var seen = new HashSet<ApplicationStatus>(application.History) { application.CurrentStatus };
            var current = application.CurrentStatus;

            var interviewed = seen.Overlaps(ApplicationStatusClassification.InterviewStatuses)
                              || seen.Overlaps(ApplicationStatusClassification.OfferStatuses);

            if (interviewed)
            {
                counts.Interviewed++;

                // An offer outranks what happened after it (declined, withdrawn): the company
                // answered with the best answer there is, and that is what the card is about.
                if (seen.Overlaps(ApplicationStatusClassification.OfferStatuses))
                {
                    counts.Offer++;
                }
                else if (current == ApplicationStatus.Rejected)
                {
                    counts.RejectedAfterInterview++;
                }
                else if (current == ApplicationStatus.Ghosted)
                {
                    counts.SilentAfterInterview++;
                }
                else if (current == ApplicationStatus.Withdrawn)
                {
                    counts.WithdrawnAfterInterview++;
                }
                else
                {
                    counts.InterviewInProgress++;
                }

                continue;
            }

            if (current == ApplicationStatus.Rejected)
            {
                counts.RejectedBeforeInterview++;
            }
            else if (current == ApplicationStatus.Withdrawn)
            {
                counts.WithdrawnBeforeInterview++;
            }
            else if (current == ApplicationStatus.Ghosted)
            {
                // Marked ghosted by the person, whether or not a screening call came first: the
                // process went quiet, which is the card's "left unanswered".
                counts.Unanswered++;
            }
            else if (seen.Overlaps(ApplicationStatusClassification.RespondedStatuses))
            {
                counts.InScreening++;
            }
            else if ((now - application.AppliedAt).TotalDays >= unansweredAfterDays)
            {
                // Same line the reminders draw for "possibly ghosted" (GhostingThresholdDays):
                // a fresh application with no reply yet is waiting, not unanswered.
                counts.Unanswered++;
            }
            else
            {
                counts.AwaitingReply++;
            }
        }

        return counts.ToRecord();
    }

    private sealed class MutableCounts
    {
        public int Total;
        public int Unanswered;
        public int AwaitingReply;
        public int RejectedBeforeInterview;
        public int InScreening;
        public int WithdrawnBeforeInterview;
        public int Interviewed;
        public int Offer;
        public int InterviewInProgress;
        public int RejectedAfterInterview;
        public int SilentAfterInterview;
        public int WithdrawnAfterInterview;

        public ApplicationFlowCounts ToRecord() => new(
            Total, Unanswered, AwaitingReply, RejectedBeforeInterview, InScreening, WithdrawnBeforeInterview,
            Interviewed, Offer, InterviewInProgress, RejectedAfterInterview, SilentAfterInterview,
            WithdrawnAfterInterview);
    }
}
