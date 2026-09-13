using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Api.Filters;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanyReviews;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Infrastructure;

namespace AfterApply.Api.Endpoints;

public static class CompanyReviewEndpoints
{
    public static IEndpointRouteBuilder MapCompanyReviewEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous, and that is the point: a company page is something a stranger reads before
        // deciding whether to apply, let alone register. No RequireAuthorization means a stale
        // bearer token a signed-in browser attaches is simply ignored rather than answered with a
        // 401 — so the web app's refresh loop never fires on a public page. Every query behind
        // these routes starts from Status == Approved (CompanyDirectoryService); nothing pending or
        // rejected has a path to this surface.
        var publicGroup = app.MapGroup("/api/companies/public").WithTags("CompanyReviews")
            .AddEndpointFilter<CompanyReviewsEnabledFilter>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        publicGroup.MapGet("/", async ([AsParameters] PublicCompanyListQuery query, ICompanyDirectoryService directory,
                CancellationToken cancellationToken) =>
            Results.Ok(await directory.ListAsync(query, cancellationToken)))
            .WithValidation<PublicCompanyListQuery>()
            .RequireRateLimiting(DependencyInjection.CompanyPublicSearchRateLimitPolicy)
            .WithSummary("Companies with at least one published review")
            .WithDescription("Public. Ordered by review count; the optional q filters by name. Companies nobody has " +
                             "reviewed yet are not listed — there is nothing on their page to read.")
            .Produces<PagedResult<CompanyPublicListItemResponse>>()
            .Produces(StatusCodes.Status429TooManyRequests);

        publicGroup.MapGet("/slugs", async (ICompanyDirectoryService directory, CancellationToken cancellationToken) =>
            Results.Ok(await directory.ListReviewedSlugsAsync(cancellationToken)))
            .WithSummary("Slugs of the companies with a published review, for the sitemap")
            .Produces<IReadOnlyList<ReviewedCompanySlugResponse>>();

        publicGroup.MapGet("/{slug}", async (string slug, ICompanyDirectoryService directory, CancellationToken cancellationToken) =>
            {
                var company = await directory.GetBySlugAsync(slug, cancellationToken);
                return company is null ? Results.NotFound() : Results.Ok(company);
            })
            .WithSummary("A company's public profile and review aggregate")
            .WithDescription("Public. The score is null until CompanyReviews:MinimumReviewsForScore reviews are " +
                             "published; the formula is documented on CompanyReviewScoring and the /companies/scoring page.")
            .Produces<CompanyPublicResponse>();

        publicGroup.MapGet("/{slug}/reviews", async (string slug, [AsParameters] PublicReviewListQuery query,
                ICompanyDirectoryService directory, CancellationToken cancellationToken) =>
            {
                var page = await directory.ListApprovedReviewsAsync(slug, query, cancellationToken);
                return page is null ? Results.NotFound() : Results.Ok(page);
            })
            .WithValidation<PublicReviewListQuery>()
            .WithSummary("Published reviews of a company")
            .WithDescription("Public and anonymous: no author, and the date is month precision.")
            .Produces<PagedResult<CompanyReviewPublicResponse>>();

        // Everything a signed-in person does. Extension tokens stay out (no AllowExtensionToken):
        // writing a review is a person at a keyboard, never a content script.
        var userGroup = app.MapGroup("/api").WithTags("CompanyReviews").RequireAuthorization()
            .AddEndpointFilter<CompanyReviewsEnabledFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        userGroup.MapPost("/companies/resolve", async (ResolveCompanyRequest request, ClaimsPrincipal user,
                ICompanyReviewService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ResolveCompanyAsync(user.GetUserId(), request.Name, cancellationToken)))
            .WithValidation<ResolveCompanyRequest>()
            .RequireRateLimiting(DependencyInjection.CompanyReviewWriteRateLimitPolicy)
            .WithSummary("Find or create a company by name so it can be reviewed")
            .WithDescription("The same normalised lookup the application form uses, so spelling variants land on " +
                             "one row. Bounded by the review-write rate limit: a created company is visible to " +
                             "every user's autocomplete.")
            .Produces<ResolvedCompanyResponse>()
            .Produces(StatusCodes.Status429TooManyRequests);

        userGroup.MapGet("/companies/{companyId:guid}/reviews/me", async (Guid companyId, ClaimsPrincipal user,
                ICompanyReviewService service, CancellationToken cancellationToken) =>
            {
                var state = await service.GetViewerStateAsync(user.GetUserId(), companyId, cancellationToken);
                return state is null ? Results.NotFound() : Results.Ok(state);
            })
            .WithSummary("The caller's own review of this company, their helpful marks here, and their quota")
            .Produces<CompanyReviewViewerStateResponse>();

        userGroup.MapPost("/companies/{companyId:guid}/reviews", async (Guid companyId, CreateCompanyReviewRequest request,
                ClaimsPrincipal user, ICompanyReviewService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(user.GetUserId(), companyId, request, cancellationToken);
                return created is null ? Results.NotFound() : Results.Created($"/api/company-reviews/{created.Id}", created);
            })
            .WithValidation<CreateCompanyReviewRequest>()
            .RequireRateLimiting(DependencyInjection.CompanyReviewWriteRateLimitPolicy)
            .WithSummary("Write a review of a company")
            .WithDescription("Stored as Pending; nothing is public until an admin approves it. One review per " +
                             "company per account, and at most CompanyReviews:MaxReviewsPerUser in total (or the " +
                             "account's override) — both refusals are 400s with a coded detail.")
            .Produces<MyCompanyReviewResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        userGroup.MapGet("/company-reviews/mine", async (ClaimsPrincipal user, ICompanyReviewService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListMineAsync(user.GetUserId(), cancellationToken)))
            .WithSummary("The caller's reviews, with moderation status and quota")
            .Produces<MyReviewsResponse>();

        userGroup.MapPut("/company-reviews/{reviewId:guid}", async (Guid reviewId, UpdateCompanyReviewRequest request,
                ClaimsPrincipal user, ICompanyReviewService service, CancellationToken cancellationToken) =>
            {
                var updated = await service.UpdateAsync(user.GetUserId(), reviewId, request, cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            })
            .WithValidation<UpdateCompanyReviewRequest>()
            .RequireRateLimiting(DependencyInjection.CompanyReviewWriteRateLimitPolicy)
            .WithSummary("Edit the caller's own review")
            .WithDescription("Sends the review back to Pending, so an edit is never public before an admin has " +
                             "read it. Another account's review is 404, not 403.")
            .Produces<MyCompanyReviewResponse>()
            .Produces(StatusCodes.Status429TooManyRequests);

        userGroup.MapDelete("/company-reviews/{reviewId:guid}", async (Guid reviewId, ClaimsPrincipal user,
                ICompanyReviewService service, CancellationToken cancellationToken) =>
            await service.DeleteAsync(user.GetUserId(), reviewId, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .RequireRateLimiting(DependencyInjection.CompanyReviewWriteRateLimitPolicy)
            .WithSummary("Delete the caller's own review")
            .Produces(StatusCodes.Status204NoContent);

        userGroup.MapPost("/company-reviews/{reviewId:guid}/helpful", async (Guid reviewId, ClaimsPrincipal user,
                ICompanyReviewService service, CancellationToken cancellationToken) =>
            {
                var result = await service.ToggleHelpfulAsync(user.GetUserId(), reviewId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireRateLimiting(DependencyInjection.CompanyReviewHelpfulRateLimitPolicy)
            .WithSummary("Toggle 'helpful' on a published review")
            .WithDescription("Idempotent per account: on, then off. Own reviews are refused with a 400.")
            .Produces<HelpfulToggleResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        userGroup.MapPost("/company-reviews/{reviewId:guid}/reports", async (Guid reviewId, ReportCompanyReviewRequest request,
                ClaimsPrincipal user, ICompanyReviewService service, CancellationToken cancellationToken) =>
            {
                var result = await service.ReportAsync(user.GetUserId(), reviewId, request, cancellationToken);
                return result is null ? Results.NotFound() : Results.Created($"/api/admin/company-review-reports/{result.Id}", result);
            })
            .WithValidation<ReportCompanyReviewRequest>()
            .RequireRateLimiting(DependencyInjection.CompanyReviewReportRateLimitPolicy)
            .WithSummary("Report a published review for moderation")
            .WithDescription("One open report per account per review. Admins see the reporter; the review's " +
                             "author never does.")
            .Produces<ReportCompanyReviewResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
