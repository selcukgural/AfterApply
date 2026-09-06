namespace AfterApply.Domain.Applications;

/// <summary>
/// Where an application's HR email address came from. The UI needs this to be honest about an
/// address the user never typed: an auto-filled one is a reasonable guess made from a real email
/// they received, not a fact they asserted, and it is labelled as such so they can tell at a
/// glance whether to trust it.
/// </summary>
public enum HrEmailSource
{
    /// <summary>Typed by the user on the application or tracked-job form.</summary>
    Manual,

    /// <summary>Read off the sender of an email that was matched to this application and then
    /// confirmed (or auto-applied) — see EmailForwardingService and HrEmailCandidate.</summary>
    IncomingEmail
}
