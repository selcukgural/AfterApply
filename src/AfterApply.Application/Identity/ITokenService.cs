namespace AfterApply.Application.Identity;

public interface ITokenService
{
    (string AccessToken, DateTimeOffset ExpiresAt) CreateAccessToken(Guid userId, string email);

    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);

    string GeneratePersonalAccessToken();

    string HashPersonalAccessToken(string token);
}
