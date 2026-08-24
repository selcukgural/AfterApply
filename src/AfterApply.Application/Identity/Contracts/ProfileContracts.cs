namespace AfterApply.Application.Identity.Contracts;

public sealed record UpdateProfileRequest(string FirstName, string LastName);

public sealed record UserProfileResponse(Guid Id, string Email, string FirstName, string LastName, DateTimeOffset CreatedAt);
