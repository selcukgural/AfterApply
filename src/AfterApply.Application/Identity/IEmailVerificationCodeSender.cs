namespace AfterApply.Application.Identity;

/// <summary>The background job that mints a verification code and emails it. It takes the
/// challenge id rather than the code, so the code is never written into the job's stored
/// arguments — it is generated, hashed onto the challenge and sent in the same run.</summary>
public interface IEmailVerificationCodeSender
{
    Task SendAsync(Guid challengeId, string locale, CancellationToken cancellationToken);
}

/// <summary>Deletes accounts that never verified their address within the retention window
/// (<c>EmailVerification:UnverifiedAccountRetentionDays</c>), plus the verification and
/// email-throttle rows that have outlived their use.</summary>
public interface IUnverifiedAccountCleanupService
{
    Task<int> PurgeAsync(CancellationToken cancellationToken);
}
