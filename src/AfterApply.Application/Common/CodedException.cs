using AfterApply.Domain.Common;

namespace AfterApply.Application.Common;

/// <summary>
/// Same contract as <c>DomainException</c> (Domain layer) for errors raised outside the domain
/// model proper — e.g. Infrastructure services rejecting a request due to missing configuration.
/// </summary>
/// <param name="messageArguments">Values the API layer substitutes into the localized text for
/// <paramref name="errorCode"/> (<c>{0}</c>, <c>{1}</c>, …) — for messages that quote a configurable
/// limit, so the resx never has to hardcode a number that lives in appsettings.</param>
public class CodedException(string errorCode, string message, params object[] messageArguments)
    : Exception(message), IHasErrorCode
{
    public string ErrorCode { get; } = errorCode;

    public IReadOnlyList<object> MessageArguments { get; } = messageArguments;
}
