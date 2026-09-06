namespace AfterApply.Application.EmailIntegrations;

/// <summary>
/// Decides whether the sender of a matched email is worth offering to the user as an HR contact.
///
/// This exists because the alternative — neither job site publishes an HR address (measured
/// 2026-09-06: 0 of 35 kariyer.net postings carried one, and LinkedIn never shows the poster's) —
/// leaves the inbox as the only place a real, reachable address ever turns up. But most of what
/// lands there is a robot: <c>no-reply@</c>, a bounce mailbox, or an ATS relay that belongs to the
/// applicant-tracking vendor rather than the company. Storing one of those as "the person to
/// contact" would be worse than storing nothing, since the user would only find out by writing to
/// it and getting silence back.
///
/// So this is the gate for both storing and offering: an address that does not pass is never
/// written down at all, not on the suggestion and not on the application. That keeps the sender
/// address — which the pipeline otherwise deliberately reduces to a bare domain — out of the
/// database except in the one case where it is something the user can actually use.
///
/// A departmental mailbox like <c>careers@</c> or <c>ik@</c> passes on purpose: it is not a named
/// person, but it is monitored by one, which is exactly what a candidate needs.
/// </summary>
public static class HrEmailCandidate
{
    private const int MaxLength = 320;

    /// <summary>Matched against the local part with its separators stripped, so "no-reply",
    /// "no.reply" and "noreply" are one entry rather than three.</summary>
    private static readonly string[] AutomatedMarkers =
    [
        "noreply", "donotreply", "nepasrepondre", "nichtantworten", "yanitlamayin",
        "mailerdaemon", "postmaster", "bounce", "autoreply", "automated", "notification"
    ];

    /// <param name="senderIsKnownJobBoard">Whether the sender's domain is on the curated job
    /// board/ATS list — those addresses belong to the tooling vendor, not to the company the user
    /// applied to, so replying to one reaches nobody who can help.</param>
    /// <returns>The normalized address, or null when it should neither be stored nor offered.</returns>
    public static string? From(string? senderEmail, bool senderIsKnownJobBoard)
    {
        if (senderIsKnownJobBoard || string.IsNullOrWhiteSpace(senderEmail))
        {
            return null;
        }

        var normalized = senderEmail.Trim().ToLowerInvariant();
        if (normalized.Length > MaxLength)
        {
            return null;
        }

        var parts = normalized.Split('@');
        if (parts.Length != 2)
        {
            return null;
        }

        var (localPart, domain) = (parts[0], parts[1]);
        if (localPart.Length == 0 || !domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.'))
        {
            return null;
        }

        // Characters that would let a stored address change the meaning of the mailto: link the web
        // app builds from it — the frontend rejects these too, and an address we would refuse to
        // render is not one worth storing.
        if (normalized.AsSpan().IndexOfAny(" \t\r\n,;:<>()[]\\?&#%\"'") >= 0)
        {
            return null;
        }

        var collapsedLocalPart = localPart.Replace(".", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
        return AutomatedMarkers.Any(marker => collapsedLocalPart.Contains(marker, StringComparison.Ordinal))
            ? null
            : normalized;
    }
}
