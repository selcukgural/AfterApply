using Microsoft.AspNetCore.Identity;

namespace AfterApply.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ConsentAcceptedAt { get; set; }

    /// <summary>ISO 639-1 code ("tr"/"en") applied to this user's session right after login,
    /// regardless of which device/browser they sign in from. Kept in sync with the frontend's
    /// current UI locale whenever the user switches languages while authenticated.</summary>
    public string PreferredLanguage { get; set; } = "tr";
}
