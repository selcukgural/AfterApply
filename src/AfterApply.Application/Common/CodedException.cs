using AfterApply.Domain.Common;

namespace AfterApply.Application.Common;

/// <summary>
/// Same contract as <c>DomainException</c> (Domain layer) for errors raised outside the domain
/// model proper — e.g. Infrastructure services rejecting a request due to missing configuration.
/// </summary>
public class CodedException(string errorCode, string message) : Exception(message), IHasErrorCode
{
    public string ErrorCode { get; } = errorCode;
}
