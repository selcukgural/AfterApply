namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// One pending email verification (2026-09-24): the ticket the signing-in browser holds and the
/// code the inbox receives, both stored as hashes. An account has at most one — starting a new
/// verification replaces the ticket, so an older tab's ticket stops working.
/// </summary>
public sealed class EmailVerificationChallenge
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public string TicketHash { get; private set; } = string.Empty;

    /// <summary>Null until the send job has minted a code.</summary>
    public string? CodeHash { get; private set; }

    public DateTimeOffset? CodeIssuedAt { get; private set; }

    public int FailedAttempts { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the ticket itself stops working, code or not.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    private EmailVerificationChallenge()
    {
    }

    public static EmailVerificationChallenge Create(Guid userId, string ticketHash, DateTimeOffset now, TimeSpan ticketLifetime) =>
        new()
        {
            UserId = userId,
            TicketHash = ticketHash,
            CreatedAt = now,
            ExpiresAt = now + ticketLifetime
        };

    public void ReplaceTicket(string ticketHash, DateTimeOffset now, TimeSpan ticketLifetime)
    {
        TicketHash = ticketHash;
        ExpiresAt = now + ticketLifetime;
    }

    /// <summary>A fresh code resets the attempt count: the limit is per code, and the resend
    /// throttle is what bounds how many codes there can be.</summary>
    public void IssueCode(string codeHash, DateTimeOffset now)
    {
        CodeHash = codeHash;
        CodeIssuedAt = now;
        FailedAttempts = 0;
    }

    public void RecordFailedAttempt() => FailedAttempts++;
}
