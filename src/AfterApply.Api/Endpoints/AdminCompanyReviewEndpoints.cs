using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Api.Filters;
using AfterApply.Application.Admin;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanyReviews;
using AfterApply.Application.CompanyReviews.Contracts;

namespace AfterApply.Api.Endpoints;

/// <summary>The moderation surface. Same access rule as AdminEndpoints — the Users.IsAdmin column,
/// re-read on every request, bare 403 otherwise — and the only place a review's author is joined
/// to a response.</summary>
public static class AdminCompanyReviewEndpoints
{
    public static IEndpointRouteBuilder MapAdminCompanyReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization()
            .AddEndpointFilter<CompanyReviewsEnabledFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/company-reviews", async ([AsParameters] AdminReviewListQuery query, ClaimsPrincipal user,
                IAdminAccessService adminAccess, ICompanyReviewModerationService moderation, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await moderation.ListAsync(query, cancellationToken));
            })
            .WithValidation<AdminReviewListQuery>()
            .WithSummary("The moderation queue")
            .WithDescription("Filter by status, company name and submission date. Pending first, oldest first.")
            .Produces<PagedResult<AdminCompanyReviewListItemResponse>>();

        group.MapGet("/company-reviews/counts", async (ClaimsPrincipal user, IAdminAccessService adminAccess,
                ICompanyReviewModerationService moderation, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await moderation.GetCountsAsync(cancellationToken));
            })
            .WithSummary("How much is waiting: pending reviews and open reports")
            .Produces<ModerationCountsResponse>();

        group.MapGet("/company-reviews/{reviewId:guid}", async (Guid reviewId, ClaimsPrincipal user, IAdminAccessService adminAccess,
                ICompanyReviewModerationService moderation, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var review = await moderation.GetAsync(reviewId, cancellationToken);
                return review is null ? Results.NotFound() : Results.Ok(review);
            })
            .WithSummary("One review with its author and its reports")
            .Produces<AdminCompanyReviewResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/company-reviews/{reviewId:guid}/approve", async (Guid reviewId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, ICompanyReviewModerationService moderation, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return await moderation.ApproveAsync(user.GetUserId(), reviewId, cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .WithSummary("Publish a review")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/company-reviews/{reviewId:guid}/reject", async (Guid reviewId, RejectCompanyReviewRequest request,
                ClaimsPrincipal user, IAdminAccessService adminAccess, ICompanyReviewModerationService moderation,
                CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return await moderation.RejectAsync(user.GetUserId(), reviewId, request.Reason, cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .WithValidation<RejectCompanyReviewRequest>()
            .WithSummary("Reject a review with a reason the author will read")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/company-review-reports", async ([AsParameters] AdminReportListQuery query, ClaimsPrincipal user,
                IAdminAccessService adminAccess, ICompanyReviewModerationService moderation, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await moderation.ListReportsAsync(query, cancellationToken));
            })
            .WithValidation<AdminReportListQuery>()
            .WithSummary("Reports readers filed against published reviews")
            .Produces<PagedResult<AdminReviewReportResponse>>();

        group.MapPost("/company-review-reports/{reportId:guid}/resolve", async (Guid reportId, ResolveReviewReportRequest request,
                ClaimsPrincipal user, IAdminAccessService adminAccess, ICompanyReviewModerationService moderation,
                CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return await moderation.ResolveReportAsync(user.GetUserId(), reportId, request, cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .WithValidation<ResolveReviewReportRequest>()
            .WithSummary("Decide a report: dismiss, request changes, or remove the review")
            .WithDescription("Dismissed leaves the review published. The other two reject it with the given " +
                             "reason and close every other open report on it.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/users/{userId:guid}/review-quota", async (Guid userId, SetReviewQuotaRequest request,
                ClaimsPrincipal user, IAdminAccessService adminAccess, ICompanyReviewModerationService moderation,
                CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var quota = await moderation.SetUserQuotaAsync(userId, request.ReviewQuotaOverride, cancellationToken);
                return quota is null ? Results.NotFound() : Results.Ok(quota);
            })
            .WithValidation<SetReviewQuotaRequest>()
            .WithSummary("Set (or clear, with null) one account's review quota")
            .Produces<UserReviewQuotaResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
