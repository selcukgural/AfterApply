using AfterApply.Application.CvScan.Contracts;
using AfterApply.Application.Documents;
using AfterApply.Infrastructure;
using AfterApply.Infrastructure.CvScan;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Endpoints;

public static class CvScanEndpoints
{
    public static IEndpointRouteBuilder MapCvScanEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous, and that is the feature rather than an oversight — the same reasoning as the
        // public benchmark. Someone who has never heard of the product arrives with a file they
        // already have, and gets an answer before being asked for anything.
        var group = app.MapGroup("/api/cv-scan").WithTags("CvScan")
            .WithDescription("Hidden behind CvScan:Enabled — the route 404s while the flag is off.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.AddEndpointFilter(async (context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<CvScanOptions>>();
            return options.Value.Enabled ? await next(context) : Results.NotFound();
        });

        // consentAccepted is bool? for the same reason it is on the CV upload endpoint: a
        // non-nullable bool bound [FromForm] throws when the part is absent, which surfaced as a
        // 500 for what is really a malformed request. Null is refused exactly like false.
        group.MapPost("/", async ([FromForm] IFormFile file, [FromForm] bool? consentAccepted,
            [FromForm] bool? contentNotesRequested, [FromForm] string? website, [FromForm] long? elapsedMs,
            [FromForm] string? locale, ICvScanService service, HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await using var stream = file.OpenReadStream();
                var result = await service.ScanAsync(
                    new CvScanRequest(stream, file.FileName, file.Length, consentAccepted ?? false,
                        // Absent means not given, for the optional consent exactly as for the
                        // required one: the only way to opt into the content notes is to say so.
                        contentNotesRequested ?? false, website, elapsedMs,
                        locale == "tr" ? "tr" : "en"),
                    cancellationToken);

                // The response carries excerpts of the caller's own CV. It is theirs and it is
                // answering their own request, but it must not sit in any shared cache on the way
                // back, and no browser should keep a copy on disk.
                httpContext.Response.Headers.CacheControl = "private, no-store";

                return Results.Ok(result);
            }
            catch (CvUploadValidationException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [exception.Field] = exception.Errors.ToArray()
                });
            }
        }).DisableAntiforgery().RequireRateLimiting(DependencyInjection.CvScanRateLimitPolicy)
            .WithSummary("Scan a CV for machine readability, without an account")
            .WithDescription("multipart/form-data with a 'file' part and a 'consentAccepted' part. " +
                             "Returns a 0-100 score, the four category subtotals it is the sum of, " +
                             "the findings behind it — each one carrying a page and an excerpt — and " +
                             "the extracted text so the caller can see what a machine reads. " +
                             "The score is computed by deterministic checks; no model participates in " +
                             "it, which is why a CV that contains instructions cannot argue with it. " +
                             "'contentNotesRequested' is a separate, optional consent: with it, and only " +
                             "with it, the extracted text is sent to a model for notes about the writing " +
                             "— which are returned outside the score and never change it. " +
                             "Nothing is stored: the file is never written to disk and the only row " +
                             "that outlives the request is an anonymous score with no identifier.")
            .Produces<CvScanResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
