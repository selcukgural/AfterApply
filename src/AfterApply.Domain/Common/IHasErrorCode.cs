namespace AfterApply.Domain.Common;

/// <summary>
/// Implemented by exceptions that carry a stable, translatable error code instead of (or in
/// addition to) a fixed English <see cref="Exception.Message"/>. The API layer's global exception
/// handler translates <see cref="ErrorCode"/> into the request's culture before it ever reaches a
/// client — <see cref="Exception.Message"/> stays English, for logs only.
/// </summary>
public interface IHasErrorCode
{
    string ErrorCode { get; }
}
