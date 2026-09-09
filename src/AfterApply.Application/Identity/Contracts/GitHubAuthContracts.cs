namespace AfterApply.Application.Identity.Contracts;

/// <summary>What the web app's GitHub callback page posts after github.com redirected back. No PKCE
/// verifier, same as <see cref="LinkedInSignInRequest"/> — GitHub's OAuth App endpoints don't accept
/// a <c>code_challenge</c>/<c>code_verifier</c> pair, so the single-use <c>state</c> the browser
/// keeps in sessionStorage is the login-CSRF defence.</summary>
public sealed record GitHubSignInRequest(string Code, string RedirectUri);

/// <summary>Second step for a GitHub account that has no e-kariyerim account yet. <see cref="Email"/>
/// is used only when the signed-in identity carried no usable verified address of its own (a GitHub
/// account can keep every address private, or expose only an undeliverable
/// <c>@users.noreply.github.com</c> one) — when it did, the client's value here is ignored and the
/// verified address is used instead.</summary>
public sealed record GitHubSignupRequest(string SignupToken, string FirstName, string LastName, string? Email, bool ConsentAccepted);

/// <summary>Returned instead of tokens when the GitHub identity is new to us. The account is NOT
/// created yet — that only happens once the user accepts the privacy policy (and, if needed,
/// supplies an email) on the follow-up form. <see cref="Email"/> is GitHub's verified address, shown
/// read-only, or null when GitHub provided none usable — in which case the form must collect and
/// require one. The two names are a best-effort split of GitHub's single free-text profile name and
/// are meant to be corrected.</summary>
public sealed record GitHubSignupPrefill(string SignupToken, string? Email, string FirstName, string LastName);

/// <summary>Exactly one of the two is non-null: <see cref="Auth"/> when an account was found (or
/// linked by verified email) and the user is signed in, <see cref="PendingSignup"/> when the client
/// has to show the complete-your-sign-up step first.</summary>
public sealed record GitHubSignInResponse(AuthResponse? Auth, GitHubSignupPrefill? PendingSignup);

public sealed record GitHubSignInResult
{
    public bool Succeeded { get; private init; }

    public GitHubSignInResponse? Response { get; private init; }

    public IReadOnlyCollection<string> Errors { get; private init; } = [];

    public static GitHubSignInResult SignedIn(AuthResponse auth) =>
        new() { Succeeded = true, Response = new GitHubSignInResponse(auth, null) };

    public static GitHubSignInResult SignupRequired(GitHubSignupPrefill prefill) =>
        new() { Succeeded = true, Response = new GitHubSignInResponse(null, prefill) };

    public static GitHubSignInResult Failure(params IReadOnlyCollection<string> errors) =>
        new() { Succeeded = false, Errors = errors };
}
