using FluentValidation;

namespace AfterApply.Application.EmailIntegrations.Validators;

/// <summary>
/// The Gmail content script already caps what it sends (gmail-scan.js: 2000 chars of body text),
/// but that is a client-side convenience, not a boundary — anyone holding a personal access token
/// can POST this shape directly. Without these limits an unbounded body went straight into a
/// Hangfire job argument (serialized into Postgres) and from there into an OpenAI prompt, so a
/// single caller could inflate both the database and the OpenAI bill at will.
///
/// The two text caps deliberately mirror EmailSuggestionConfiguration's column lengths
/// (Subject 500, Snippet 2000). Those are the values that ultimately have to hold: oversized text
/// used to get past this endpoint and only fail later at SaveChangesAsync, inside the background
/// job, where it turned into ten Hangfire retries of a request that could never succeed. Rejecting
/// it here with a 400 tells the caller what actually went wrong.
/// </summary>
public sealed class ExtensionEmailSignalRequestValidator : AbstractValidator<ExtensionEmailSignalRequest>
{
    // RFC 5321's maximum reverse-path length.
    private const int MaxEmailLength = 320;

    // A domain name can't exceed 253 characters; the list itself is bounded because a single
    // marketing email can legitimately carry a few dozen distinct link hosts, but not hundreds.
    private const int MaxLinkDomainLength = 253;
    private const int MaxLinkDomains = 50;

    public ExtensionEmailSignalRequestValidator()
    {
        RuleFor(x => x.SenderEmail).NotEmpty().EmailAddress().MaximumLength(MaxEmailLength);
        RuleFor(x => x.SenderDisplayName).MaximumLength(300);
        RuleFor(x => x.Subject).MaximumLength(500);
        RuleFor(x => x.Snippet).MaximumLength(2000);

        // ProviderMessageId is a hash of this (ComputeIdempotencyKey), so the stored value is
        // fixed-width regardless — the cap is about what we're willing to accept and log, not
        // about fitting the column.
        RuleFor(x => x.GmailMessageId).NotEmpty().MaximumLength(256);

        // Nothing rejects a future timestamp downstream, and it would sort a suggestion to the top
        // of the notifications list forever. Same one-day tolerance as CreateApplicationRequest's
        // AppliedAt rule, for the same clock-skew reason.
        RuleFor(x => x.ReceivedAt).LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddDays(1));

        RuleFor(x => x.LinkDomains).NotNull();
        RuleFor(x => x.LinkDomains.Count).LessThanOrEqualTo(MaxLinkDomains)
            .When(x => x.LinkDomains.Count != 0);
        RuleForEach(x => x.LinkDomains).NotEmpty().MaximumLength(MaxLinkDomainLength)
            .When(x => x.LinkDomains.Count != 0);
    }
}
