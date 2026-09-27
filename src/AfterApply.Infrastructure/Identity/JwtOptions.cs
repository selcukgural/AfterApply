namespace AfterApply.Infrastructure.Identity;

public sealed class JwtOptions
{
    public required string SigningKey { get; init; }

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required int AccessTokenMinutes { get; init; }

    public required int RefreshTokenDays { get; init; }

    /// <summary>The longest a session lives from its sign-in, however often it is refreshed.</summary>
    public int AbsoluteSessionDays { get; init; } = 90;
}
