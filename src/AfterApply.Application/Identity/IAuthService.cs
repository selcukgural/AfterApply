using AfterApply.Application.Identity.Contracts;

namespace AfterApply.Application.Identity;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<AuthResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);

    Task<UserProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserProfileResponse?> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);

    Task<UserProfileResponse?> UpdateLanguageAsync(Guid userId, string language, CancellationToken cancellationToken);

    /// <summary>Returns false when the supplied password does not match the account (the account owner is
    /// already established via the authenticated userId, so false unambiguously means "wrong password").</summary>
    Task<bool> DeleteAccountAsync(Guid userId, string password, CancellationToken cancellationToken);

    Task<AccountExportResponse> ExportAccountDataAsync(Guid userId, CancellationToken cancellationToken);
}
