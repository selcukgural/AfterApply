namespace AfterApply.Domain.Common;

/// <remarks>
/// Message carries a fixed English technical description for logs only — it is never shown to
/// users. User-facing text is produced at the API boundary by translating <see cref="ErrorCode"/>.
/// </remarks>
public abstract class DomainException(string errorCode, string message) : Exception(message), IHasErrorCode
{
    public string ErrorCode { get; } = errorCode;
}
