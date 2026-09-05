namespace AfterApply.Application.Identity.Contracts;

/// <summary>What the web app's Google callback page posts after accounts.google.com redirected
/// back to it. <see cref="CodeVerifier"/> is the PKCE secret the page generated before redirecting
/// to Google; <see cref="RedirectUri"/> must be the exact URI used in that redirect (Google
/// re-checks it at exchange time).</summary>
public sealed record GoogleSignInRequest(string Code, string CodeVerifier, string RedirectUri);

/// <summary>Second step for a Google account that has no e-kariyerim account yet: the signed
/// <see cref="SignupToken"/> from <see cref="GoogleSignupPrefill"/> carries the verified Google
/// identity, the rest is what the user confirmed on the "complete your sign-up" form.</summary>
public sealed record GoogleSignupRequest(string SignupToken, string FirstName, string LastName, bool ConsentAccepted);

/// <summary>Returned instead of tokens when the Google identity is new to us. The account is NOT
/// created yet — that only happens once the user accepts the privacy policy on the follow-up form,
/// exactly as a password sign-up requires. Email/names are Google's values, shown pre-filled.</summary>
public sealed record GoogleSignupPrefill(string SignupToken, string Email, string FirstName, string LastName);

/// <summary>Exactly one of the two is non-null: <see cref="Auth"/> when an account was found (or
/// linked by verified email) and the user is signed in, <see cref="PendingSignup"/> when the
/// client has to show the complete-your-sign-up step first.</summary>
public sealed record GoogleSignInResponse(AuthResponse? Auth, GoogleSignupPrefill? PendingSignup);

public sealed record GoogleSignInResult
{
    public bool Succeeded { get; private init; }

    public GoogleSignInResponse? Response { get; private init; }

    public IReadOnlyCollection<string> Errors { get; private init; } = [];

    public static GoogleSignInResult SignedIn(AuthResponse auth) =>
        new() { Succeeded = true, Response = new GoogleSignInResponse(auth, null) };

    public static GoogleSignInResult SignupRequired(GoogleSignupPrefill prefill) =>
        new() { Succeeded = true, Response = new GoogleSignInResponse(null, prefill) };

    public static GoogleSignInResult Failure(params IReadOnlyCollection<string> errors) =>
        new() { Succeeded = false, Errors = errors };
}
