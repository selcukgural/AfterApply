using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.JobSearch;
using AfterApply.Application.JobSearch.Contracts;
using AfterApply.Infrastructure;
using AfterApply.Infrastructure.JobSearch;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Endpoints;

public static class JobSearchEndpoints
{
    public static IEndpointRouteBuilder MapJobSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/job-search").WithTags("JobSearch").RequireAuthorization()
            .WithDescription("Hidden behind JobSearch:Enabled — every route 404s while the flag is off.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting(DependencyInjection.JobSearchRateLimitPolicy);

        // Group-level so it runs before each route's validation filter: a request to a feature
        // that is off gets a 404, not a validation error that reveals the route exists.
        group.AddEndpointFilter(async (context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<JobSearchOptions>>();
            return options.Value.Enabled ? await next(context) : Results.NotFound();
        });

        group.MapGet("/jobs", async ([AsParameters] SearchJobsQuery query, ClaimsPrincipal user,
                IJobSearchService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.SearchAsync(user.GetUserId(), query, cancellationToken)))
            .WithValidation<SearchJobsQuery>()
            .WithSummary("Search job postings (JSearch)")
            .WithDescription("Free-text search over public job postings via JSearch. Each page of up to 10 results " +
                             "costs one credit against the caller's daily allowance and the product's monthly quota; " +
                             "numPages is clamped to the server cap (JobSearch:MaxPagesPerSearch or the user's override), " +
                             "so page with numPages=1 and the returned cursor rather than asking for many pages. " +
                             "country falls back to the caller's saved default, then to \"tr\"; language is only sent " +
                             "when the caller or their settings name one. An identical search within the cache window " +
                             "is answered from our own tables at no cost (meta.fromCache).")
            .Produces<JobSearchResultsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/jobs/details", async ([AsParameters] GetJobDetailsQuery query, ClaimsPrincipal user,
                IJobSearchService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetJobDetailsAsync(user.GetUserId(), query, cancellationToken)))
            .WithValidation<GetJobDetailsQuery>()
            .WithSummary("Full details for one or more postings by job id")
            .WithDescription("ids is a comma-separated list of job ids from a search (query string, because the ids " +
                             "are base64 and carry '='). Postings already on file are served from our own tables; " +
                             "only the missing ones go upstream, one credit each. At most JobSearch:MaxJobIdsPerDetails " +
                             "ids per call (or the user's override). Ids the provider no longer knows are omitted.")
            .Produces<JobSearchJobDetailsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/salary", async ([AsParameters] EstimatedSalaryQuery query, ClaimsPrincipal user,
                IJobSearchService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetEstimatedSalaryAsync(user.GetUserId(), query, cancellationToken)))
            .WithValidation<EstimatedSalaryQuery>()
            .WithSummary("Salary estimate for a job title around a location")
            .WithDescription("One credit per new lookup; the answer is cached for JobSearch:SalaryCacheHours and " +
                             "shared across users.")
            .Produces<JobSearchSalaryEstimatesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/company-salary", async ([AsParameters] CompanyJobSalaryQuery query, ClaimsPrincipal user,
                IJobSearchService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetCompanyJobSalaryAsync(user.GetUserId(), query, cancellationToken)))
            .WithValidation<CompanyJobSalaryQuery>()
            .WithSummary("Salary estimate for a job title at a named company")
            .WithDescription("One credit per new lookup; the answer is cached for JobSearch:SalaryCacheHours and " +
                             "shared across users.")
            .Produces<JobSearchCompanySalariesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/usage", async (ClaimsPrincipal user, IJobSearchService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetUsageAsync(user.GetUserId(), cancellationToken)))
            .WithSummary("The caller's job search credits: today against their daily limit, and the shared month")
            .Produces<JobSearchUsageResponse>();

        group.MapGet("/settings", async (ClaimsPrincipal user, IJobSearchSettingsService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetMineAsync(user.GetUserId(), cancellationToken)))
            .WithSummary("The caller's job search settings: effective values, their own overrides, the global defaults")
            .Produces<JobSearchSettingsResponse>();

        group.MapPut("/settings", async (UpdateJobSearchPreferencesRequest request, ClaimsPrincipal user,
                IJobSearchSettingsService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.UpdateMineAsync(user.GetUserId(), request, cancellationToken)))
            .WithValidation<UpdateJobSearchPreferencesRequest>()
            .WithSummary("Update the caller's job search preferences")
            .WithDescription("Default country, language, location, date filter and remote-only. Null clears an " +
                             "override back to the global default. The credit and page limits are not here — an " +
                             "admin sets those on /api/admin/job-search/settings/{userId}.")
            .Produces<JobSearchSettingsResponse>();

        return app;
    }
}
