using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Documents;
using AfterApply.Application.Documents.Contracts;
using AfterApply.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AfterApply.Api.Endpoints;

public static class CvDocumentEndpoints
{
    public static IEndpointRouteBuilder MapCvDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cv-documents").WithTags("CvDocuments").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/", async (ClaimsPrincipal user, ICvDocumentService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(user.GetUserId(), cancellationToken)))
            .WithSummary("List the current user's stored CVs")
            .WithDescription("Metadata only — the bytes come from GET /api/cv-documents/{id}/content. " +
                             "maxCount is the server's own per-user cap, so the client never has to " +
                             "keep its own copy of the number.")
            .Produces<CvDocumentListResponse>();

        // consentAccepted is bool?, not bool, on purpose. A non-nullable bool bound [FromForm]
        // throws when the field is absent, and that surfaced as a 500 — a malformed request
        // answered with "something broke on our side", which is both wrong and Sentry noise.
        // Null means "not given", which the service refuses the same way an explicit false is
        // refused: a localized 400 pointing at the checkbox.
        group.MapPost("/", async ([FromForm] IFormFile file, [FromForm] bool? consentAccepted,
            ClaimsPrincipal user, ICvDocumentService service, CancellationToken cancellationToken) =>
        {
            try
            {
                await using var stream = file.OpenReadStream();
                var created = await service.UploadAsync(user.GetUserId(), stream, file.FileName, file.Length,
                    consentAccepted ?? false, cancellationToken);

                return Results.Created($"/api/cv-documents/{created.Id}", created);
            }
            catch (CvUploadValidationException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [exception.Field] = exception.Errors.ToArray()
                });
            }
        }).DisableAntiforgery().RequireRateLimiting(DependencyInjection.UploadRateLimitPolicy)
            .WithSummary("Upload a CV")
            .WithDescription("multipart/form-data with a 'file' part and a 'consentAccepted' part. " +
                             "consentAccepted must be true: a CV can carry special-category personal " +
                             "data (KVKK art. 6), so explicit consent is the lawful basis for storing " +
                             "it and is recorded per upload — an omitted or false value is refused " +
                             "before the file is read. The extension, the size and the file's own " +
                             "leading bytes are all checked server-side too; a file whose contents " +
                             "are not the format its name claims is refused. Exceeding the per-user " +
                             "cap answers 400 with CV_DOCUMENT_LIMIT_REACHED.")
            .Produces<CvDocumentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{id:guid}/content", async (Guid id, ClaimsPrincipal user, ICvDocumentService service,
            HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var content = await service.OpenAsync(user.GetUserId(), id, cancellationToken);
            if (content is null)
            {
                return Results.NotFound();
            }

            // A CV is one user's personal document: it must not sit in any shared cache, and the
            // browser should not keep a copy on disk either.
            httpContext.Response.Headers.CacheControl = "private, no-store";

            // fileDownloadName is what makes this an attachment: Results.File writes a
            // Content-Disposition of "attachment" (with the RFC 6266 filename* encoding a Turkish
            // file name needs), so the browser saves the file rather than rendering it. Nothing
            // here is ever served inline — combined with the API's nosniff header, that is what
            // keeps an uploaded file from being interpreted as a document on our own origin.
            return Results.File(content.Content, content.ContentType, content.FileName);
        })
            .WithSummary("Download a stored CV")
            .WithDescription("Always an attachment, never inline. Proxied through the API rather than " +
                             "served from a signed URL, so every download goes through the same " +
                             "authentication and ownership check as the rest of the API.")
            .Produces<IResult>(StatusCodes.Status200OK, "application/pdf")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, ICvDocumentService service,
            CancellationToken cancellationToken) =>
        {
            var deleted = await service.DeleteAsync(user.GetUserId(), id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
            .WithSummary("Delete a stored CV")
            .WithDescription("Permanent — the file is removed from object storage as well. " +
                             "Applications that recorded this CV keep their history; their reference " +
                             "is simply cleared.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/default", async (Guid id, ClaimsPrincipal user, ICvDocumentService service,
            CancellationToken cancellationToken) =>
        {
            var updated = await service.SetDefaultAsync(user.GetUserId(), id, cancellationToken);
            return updated is not null ? Results.Ok(updated) : Results.NotFound();
        })
            .WithSummary("Make a CV the default")
            .WithDescription("The default is pre-selected on the new-application form. Setting one " +
                             "clears the previous default; a user with any CV always has exactly one.")
            .Produces<CvDocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
