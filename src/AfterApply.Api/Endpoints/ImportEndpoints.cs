using System.Security.Claims;
using System.Text.Json;
using AfterApply.Api.Extensions;
using AfterApply.Application.Imports;
using AfterApply.Application.Imports.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Infrastructure;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AfterApply.Api.Endpoints;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/imports").WithTags("Imports").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/csv", async ([FromForm] IFormFile file, [FromForm] string? columnMapping,
            ClaimsPrincipal user, IImportService service, IBackgroundJobClient jobClient,
            IStringLocalizer<SharedStrings> localizer, CancellationToken cancellationToken) =>
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
                    ["columnMapping"] = [localizer["IMPORT_COLUMN_MAPPING_INVALID_JSON"]]
                });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var staged = await service.StageCsvImportAsync(user.GetUserId(), stream, file.FileName, file.Length,
                    mapping, cancellationToken);

                jobClient.Enqueue<IImportService>(s =>
                    s.ProcessCsvImportAsync(staged.BatchId, staged.StagedFilePath, mapping, CancellationToken.None));

                return Results.Accepted($"/api/imports/{staged.BatchId}", new ImportAcceptedResponse(staged.BatchId));
            }
            catch (CsvImportValidationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ex.Errors.ToArray() });
            }
        }).DisableAntiforgery().RequireRateLimiting(DependencyInjection.UploadRateLimitPolicy)
            .WithSummary("Import applications from a CSV file")
            .WithDescription("columnMapping is an optional JSON object (as a form field) mapping CSV header names to " +
                             "AfterApply fields; omit it to use the default header names. Processing runs in the " +
                             "background — poll GET /api/imports/{id} or subscribe to /hubs/import-progress for status.")
            .Produces<ImportAcceptedResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/linkedin", async ([FromForm] IFormFile file, ClaimsPrincipal user,
            IImportService service, IBackgroundJobClient jobClient, CancellationToken cancellationToken) =>
        {
            try
            {
                await using var stream = file.OpenReadStream();
                var staged = await service.StageLinkedInZipImportAsync(user.GetUserId(), stream, file.FileName,
                    file.Length, cancellationToken);

                jobClient.Enqueue<IImportService>(s =>
                    s.ProcessLinkedInZipAsync(staged.BatchId, staged.StagedFilePath, CancellationToken.None));

                return Results.Accepted($"/api/imports/{staged.BatchId}", new ImportAcceptedResponse(staged.BatchId));
            }
            catch (CsvImportValidationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ex.Errors.ToArray() });
            }
        }).DisableAntiforgery().RequireRateLimiting(DependencyInjection.UploadRateLimitPolicy)
            .WithSummary("Import applications from a LinkedIn data export ZIP")
            .WithDescription("Expects the ZIP LinkedIn gives you from Settings > Get a copy of your data — " +
                             "looks for Job Applications*.csv files inside it. Processing runs in the background — " +
                             "poll GET /api/imports/{id} or subscribe to /hubs/import-progress for status.")
            .Produces<ImportAcceptedResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IImportService service, CancellationToken cancellationToken) =>
        {
            var summary = await service.GetByIdAsync(user.GetUserId(), id, cancellationToken);
            return summary is not null ? Results.Ok(summary) : Results.NotFound();
        })
            .WithSummary("Get the live status (or final summary) of an import batch")
            .Produces<ImportSummaryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
