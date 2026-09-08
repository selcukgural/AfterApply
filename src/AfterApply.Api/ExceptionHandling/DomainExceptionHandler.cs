using AfterApply.Application.Common;
using AfterApply.Application.Localization;
using AfterApply.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace AfterApply.Api.ExceptionHandling;

/// <summary>
/// Catches any exception carrying <see cref="IHasErrorCode"/> (domain rule violations, or
/// Infrastructure-level <c>CodedException</c>s) and turns it into a localized 400 ProblemDetails
/// response instead of an unhandled 500. Also catches the framework's own
/// <see cref="BadHttpRequestException"/>, so a request the server could not even read is answered as
/// the client error it is. Anything else falls through to ASP.NET Core's default
/// <c>UseExceptionHandler()</c> behavior (generic 500 ProblemDetails, no internals leaked).
/// </summary>
internal sealed class DomainExceptionHandler(IStringLocalizer<SharedStrings> localizer) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // A query string the model binder cannot read — `?status=Nonsense`, an enum value from an
        // old bookmark — is the caller's mistake, and BadHttpRequestException already carries the
        // status code that says so. Nothing was reading it, so those came back as unhandled 500s:
        // an alert-worthy server fault raised by anyone able to edit a URL. Its own message names
        // the parameter and echoes the value back, so it is answered with our own text rather than
        // passed through.
        if (exception is BadHttpRequestException badRequest)
        {
            await WriteProblemAsync(httpContext, badRequest.StatusCode, localizer["REQUEST_MALFORMED"], cancellationToken);
            return true;
        }

        if (exception is not IHasErrorCode codedException)
        {
            return false;
        }

        var detail = exception is CodedException { MessageArguments.Count: > 0 } coded
            ? localizer[coded.ErrorCode, coded.MessageArguments.ToArray()]
            : localizer[codedException.ErrorCode];

        await WriteProblemAsync(httpContext, StatusCodes.Status400BadRequest, detail, cancellationToken);
        return true;
    }

    private static async Task WriteProblemAsync(HttpContext httpContext, int statusCode, string detail,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            // Read off the status rather than hardcoded: a BadHttpRequestException does not always
            // carry 400 (a body over the size limit arrives as 413), and a "Bad Request" title on a
            // 413 would contradict the status beside it.
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = detail
        }, cancellationToken);
    }
}
