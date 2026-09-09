using AfterApply.Application.Identity.Contracts;

namespace AfterApply.Application.Identity;

/// <summary>
/// The six-step setup this replaces (register → settings → generate → copy → extension options →
/// paste) put the product's highest effort threshold in front of its lowest one. The flow here is
/// the device-authorization shape: the extension starts a request and polls it, the person
/// confirms it once in a browser they are already signed into, and the token travels between the
/// two without ever being displayed.
/// </summary>
public interface IExtensionPairingService
{
    /// <summary>Anonymous — the caller is an extension that has, by definition, no credential yet.
    /// </summary>
    Task<StartedExtensionPairingResponse> StartAsync(StartExtensionPairingRequest request, CancellationToken cancellationToken);

    /// <summary>What the extension calls on a timer. Mints and returns the personal access token on
    /// the first poll after approval, and never again.</summary>
    Task<ExtensionPairingPollResponse?> PollAsync(string deviceSecret, CancellationToken cancellationToken);

    /// <summary>What the verification page reads before it asks the user to confirm. Null when the
    /// code is unknown — an unknown code and an expired one are told apart, because "you waited too
    /// long, start again in the extension" and "that code does not exist" send the user to
    /// different places.</summary>
    Task<ExtensionPairingReviewResponse?> GetForReviewAsync(string code, CancellationToken cancellationToken);

    /// <summary>The one click. Records who approved; the token itself is minted when the extension
    /// polls, so a request nobody collects leaves no credential behind.</summary>
    Task<ExtensionPairingStatus?> ApproveAsync(Guid userId, string code, CancellationToken cancellationToken);

    /// <summary>"This wasn't me." Terminal, and deliberately available to any signed-in user who
    /// lands on the page with a code they did not expect.</summary>
    Task<ExtensionPairingStatus?> DenyAsync(string code, CancellationToken cancellationToken);
}
