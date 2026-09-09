using AfterApply.Application.Identity;

namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// Turns what GitHub's REST API hands back — <c>GET /user</c> plus <c>GET /user/emails</c> — into a
/// <see cref="GitHubIdentity"/>. Pure and separate from <see cref="GitHubAuthClient"/> on purpose:
/// the two decisions here (which address counts as this account's email, and how one free-text name
/// becomes a first/last pair) are the whole of GitHub's difference from the other providers, and
/// both are worth testing without an HTTP round-trip.
/// </summary>
public static class GitHubProfileReader
{
    /// <summary>GitHub's commit-privacy address. Unique and stable, but nothing sent to it is ever
    /// delivered, so an account created under one could never receive a password reset or a
    /// reminder — it is dropped rather than accepted as this user's email.</summary>
    private const string NoReplySuffix = "@users.noreply.github.com";

    public static GitHubIdentity Read(GitHubProfile profile, IReadOnlyList<GitHubEmail>? emails)
    {
        var email = SelectEmail(emails);
        var (givenName, familyName) = SplitName(profile.Name, profile.Login);

        return new GitHubIdentity(
            // The numeric id, never the login: GitHub lets an account rename itself and then frees
            // the old login for anyone else to claim, so a login as the external key would hand a
            // renamed user's account to whoever took their name.
            profile.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            email,
            email is not null,
            givenName,
            familyName);
    }

    /// <summary>
    /// The address this account may be matched to an existing e-kariyerim user by, or null when
    /// there is none. Only a GitHub-verified address qualifies — the same rule Google's
    /// <c>email_verified</c> and LinkedIn's optional email get, and for the same reason: matching on
    /// an unverified address would let anyone claim an existing account by adding its email to their
    /// own GitHub profile.
    ///
    /// The primary one wins when it qualifies, because that is the address the owner considers
    /// theirs; otherwise the first other verified address, which is just as proven. A null here is
    /// not an error — it sends the user through the same manual-email sign-up step LinkedIn already
    /// has (private-email GitHub accounts are common, and <c>GET /user</c>'s own <c>email</c> field
    /// is null for all of them).
    /// </summary>
    public static string? SelectEmail(IReadOnlyList<GitHubEmail>? emails)
    {
        if (emails is null)
        {
            return null;
        }

        var usable = emails
            .Where(e => e.Verified && !string.IsNullOrWhiteSpace(e.Email) && !IsNoReply(e.Email))
            .ToList();

        return usable.FirstOrDefault(e => e.Primary)?.Email ?? usable.FirstOrDefault()?.Email;
    }

    private static bool IsNoReply(string email) =>
        email.EndsWith(NoReplySuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// GitHub stores one free-text display name where the other two providers hand over a
    /// given/family pair, so this splits on the last space: everything before it is the first name,
    /// the last word is the surname ("Augusta Ada King" → "Augusta Ada" + "King"). That is a guess,
    /// not a fact — which is fine, because both fields land in editable inputs on the sign-up form
    /// and the user corrects them before the account exists.
    ///
    /// A profile with no name at all falls back to the login for the first name (better than an
    /// empty form) and leaves the surname empty for the user to fill in.
    /// </summary>
    public static (string? GivenName, string? FamilyName) SplitName(string? name, string? login)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return (NullIfEmpty(login?.Trim()), null);
        }

        var lastSpace = trimmed.LastIndexOf(' ');
        if (lastSpace < 0)
        {
            return (trimmed, null);
        }

        return (trimmed[..lastSpace].TrimEnd(), NullIfEmpty(trimmed[(lastSpace + 1)..]));
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>The fields of GitHub's <c>GET /user</c> response this flow reads. Deliberately not the
/// whole payload: nothing else about the account is any of our business.</summary>
public sealed record GitHubProfile(long Id, string? Login, string? Name);

/// <summary>One entry of GitHub's <c>GET /user/emails</c> response.</summary>
public sealed record GitHubEmail(string Email, bool Primary, bool Verified);
