namespace AfterApply.Application.Identity.Contracts;

/// <summary>What the web app's LinkedIn callback page posts after linkedin.com redirected back. No
/// PKCE verifier (unlike <see cref="GoogleSignInRequest"/>) — LinkedIn's authorization/token
/// endpoints don't call for one from a confidential, client-secret-holding client.</summary>
public sealed record LinkedInSignInRequest(string Code, string RedirectUri);

/// <summary>Second step for a LinkedIn account that has no e-kariyerim account yet. <see cref="Email"/>
/// is used only when the signed-in identity carried no verified email of its own (LinkedIn's OpenID
/// Connect response makes email optional) — when it did, the client's value here is ignored and the
/// token's own verified email is used instead.</summary>
public sealed record LinkedInSignupRequest(string SignupToken, string FirstName, string LastName, string? Email, bool ConsentAccepted);

/// <summary>Returned instead of tokens when the LinkedIn identity is new to us. The account is NOT
/// created yet — that only happens once the user accepts the privacy policy (and, if needed, supplies
/// an email) on the follow-up form. <see cref="Email"/> is LinkedIn's verified address, shown
/// read-only, or null when LinkedIn provided none — in which case the form must collect and require
/// one.</summary>
public sealed record LinkedInSignupPrefill(string SignupToken, string? Email, string FirstName, string LastName);

/// <summary>Exactly one of the two is non-null: <see cref="Auth"/> when an account was found (or
/// linked by verified email) and the user is signed in, <see cref="PendingSignup"/> when the client
/// has to show the complete-your-sign-up step first.</summary>
public sealed record LinkedInSignInResponse(AuthResponse? Auth, LinkedInSignupPrefill? PendingSignup);

public sealed record LinkedInSignInResult
{
    public bool Succeeded { get; private init; }

    public LinkedInSignInResponse? Response { get; private init; }

    public IReadOnlyCollection<string> Errors { get; private init; } = [];

    public static LinkedInSignInResult SignedIn(AuthResponse auth) =>
        new() { Succeeded = true, Response = new LinkedInSignInResponse(auth, null) };

    public static LinkedInSignInResult SignupRequired(LinkedInSignupPrefill prefill) =>
        new() { Succeeded = true, Response = new LinkedInSignInResponse(null, prefill) };

    public static LinkedInSignInResult Failure(params IReadOnlyCollection<string> errors) =>
        new() { Succeeded = false, Errors = errors };
}
