using AfterApply.Application.Localization;
using AfterApply.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AfterApply.Api.ExceptionHandling;

/// <summary>
/// Catches any exception carrying <see cref="IHasErrorCode"/> (domain rule violations, or
/// Infrastructure-level <c>CodedException</c>s) and turns it into a localized 400 ProblemDetails
/// response instead of an unhandled 500. Anything else falls through to ASP.NET Core's default
/// <c>UseExceptionHandler()</c> behavior (generic 500 ProblemDetails, no internals leaked).
/// </summary>
internal sealed class DomainExceptionHandler(IStringLocalizer<SharedStrings> localizer) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not IHasErrorCode codedException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = localizer[codedException.ErrorCode]
        }, cancellationToken);

        return true;
    }
}
