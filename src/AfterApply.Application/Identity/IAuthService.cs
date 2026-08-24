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
}
