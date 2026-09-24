namespace AfterApply.Application.Identity.Contracts;

/// <summary>
/// Returned instead of tokens while the account's email address is unverified (2026-09-24): after
/// a password sign-up, a sign-in to an account that never verified, or a provider sign-up whose
/// address the provider did not vouch for. A six-digit code has been (or is about to be) emailed;
/// the client posts it with <see cref="VerificationTicket"/> to <c>/api/auth/verify-email</c>.
///
/// The ticket is what binds the code to this sign-in: the code alone is worthless to whoever reads
/// the inbox, and the ticket alone is worthless to whoever holds the browser — so a sign-up made
/// under somebody else's address can never be completed by the one who made it.
/// </summary>
public sealed record EmailVerificationPendingResponse(string VerificationTicket, string Email, DateTimeOffset ResendAvailableAt);

public sealed record VerifyEmailRequest(string VerificationTicket, string Code);

public sealed record ResendVerificationCodeRequest(string VerificationTicket);

public sealed record ResendVerificationCodeResponse(DateTimeOffset ResendAvailableAt);
