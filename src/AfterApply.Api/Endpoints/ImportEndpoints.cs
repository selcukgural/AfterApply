using System.Security.Claims;
using System.Text.Json;
using AfterApply.Api.Extensions;
using AfterApply.Application.Imports;
using AfterApply.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AfterApply.Api.Endpoints;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/imports").WithTags("Imports").RequireAuthorization();

        group.MapPost("/csv", async ([FromForm] IFormFile file, [FromForm] string? columnMapping,
            ClaimsPrincipal user, IImportService service, CancellationToken cancellationToken) =>
        {
            Dictionary<string, string>? mapping;
            try
            {
                mapping = string.IsNullOrWhiteSpace(columnMapping)
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(columnMapping);
            }
            catch (JsonException)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["columnMapping"] = ["columnMapping geçerli bir JSON nesnesi olmalı, örn. {\"CompanyName\":\"Firma\"}."]
                });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var summary = await service.ImportCsvAsync(user.GetUserId(), stream, file.FileName, file.Length, mapping, cancellationToken);
                return Results.Ok(summary);
            }
            catch (CsvImportValidationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ex.Errors.ToArray() });
            }
        }).DisableAntiforgery().RequireRateLimiting(DependencyInjection.UploadRateLimitPolicy);

        group.MapPost("/linkedin", async ([FromForm] IFormFile file, ClaimsPrincipal user,
            IImportService service, CancellationToken cancellationToken) =>
        {
            try
            {
                await using var stream = file.OpenReadStream();
                var summary = await service.ImportLinkedInZipAsync(user.GetUserId(), stream, file.FileName, file.Length, cancellationToken);
                return Results.Ok(summary);
            }
            catch (CsvImportValidationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ex.Errors.ToArray() });
            }
        }).DisableAntiforgery().RequireRateLimiting(DependencyInjection.UploadRateLimitPolicy);

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IImportService service, CancellationToken cancellationToken) =>
        {
            var summary = await service.GetByIdAsync(user.GetUserId(), id, cancellationToken);
            return summary is not null ? Results.Ok(summary) : Results.NotFound();
        });

        return app;
    }
}
