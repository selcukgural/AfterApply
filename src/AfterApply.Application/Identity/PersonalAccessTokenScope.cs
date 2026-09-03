namespace AfterApply.Application.Identity;

/// <summary>
/// How much of the API a personal access token may reach. Lives in Application (not next to the
/// PersonalAccessToken entity in Infrastructure) because the request/response contracts carry it
/// and Application can't reference Infrastructure.
/// </summary>
public enum PersonalAccessTokenScope
{
    /// <summary>Everything the owning user can do through a normal browser session. What every
    /// token issued before scoping existed was implicitly granted, so the migration backfills
    /// existing rows with this value rather than silently narrowing tokens already in the wild.</summary>
    Full = 0,

    /// <summary>Only the endpoints the browser extension actually calls (see
    /// ExtensionTokenEndpointExtensions' call sites). This is the default for newly issued tokens:
    /// the extension is the only consumer, and its token sits in chrome.storage.local — readable by
    /// anyone with the profile, and loaded into the Gmail content script's world on every
    /// mail.google.com page — so a leak of it should not also hand over the account's full
    /// application history via /api/users/me/export.</summary>
    Extension = 1
}
