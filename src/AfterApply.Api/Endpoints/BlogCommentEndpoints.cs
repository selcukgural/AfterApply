using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Api.Filters;
using AfterApply.Application.Admin;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Blog;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.Blog;
using AfterApply.Infrastructure;
using Microsoft.Extensions.Localization;

namespace AfterApply.Api.Endpoints;

/// <summary>
/// Reader comments on blog posts (DECISIONS.md 2026-09-20), in the blog's three tiers: the list is
/// public (a stale token is ignored, a valid one personalises), writing needs an account, and
/// moderation needs the admin column. Every write leaves a RequestAudit row with the caller's
/// IP like any other POST — that row is the only place an address is kept, and it is never on
/// the wire (CLAUDE.md "Request audit"). Nothing here deletes: a comment's author cannot remove
/// it, and an admin rejects rather than deletes.
/// </summary>
public static class BlogCommentEndpoints
{
    public static IEndpointRouteBuilder MapBlogCommentEndpoints(this IEndpointRouteBuilder app)
    {
        // ---- public: reading -------------------------------------------------------------------
        var publicGroup = app.MapGroup("/api/blog/public").WithTags("Blog")
            .AddEndpointFilter<BlogEnabledFilter>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        publicGroup.MapGet("/posts/{postId:guid}/comments", async (Guid postId, [AsParameters] PublicBlogCommentListQuery query,
                ClaimsPrincipal user, IBlogCommentService service, CancellationToken cancellationToken) =>
            {
                var list = await service.ListAsync(postId, user.TryGetUserId(), query, cancellationToken);
                return list is null ? Results.NotFound() : Results.Ok(list);
            })
            .WithValidation<PublicBlogCommentListQuery>()
            .WithSummary("A published post's approved comments, newest first, replies under their root")
            .WithDescription("Public. Ten root comments a page, each with all its replies. With a valid token the " +
                             "caller's own pending comments are included (for them alone), isMine and helpfulByMe " +
                             "are set; without one they are absent and null. Author names are first name plus last " +
                             "initial, or null for an account with no name — nothing else about an account travels.")
            .Produces<BlogCommentListResponse>();

        // ---- signed in: writing ----------------------------------------------------------------
        var userGroup = app.MapGroup("/api/blog").WithTags("Blog").RequireAuthorization()
            .AddEndpointFilter<BlogEnabledFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        userGroup.MapPost("/posts/{postId:guid}/comments", async (Guid postId, CreateBlogCommentRequest request, ClaimsPrincipal user,
                IBlogCommentService service, CancellationToken cancellationToken) =>
            {
                var comment = await service.CreateAsync(user.GetUserId(), postId, request, cancellationToken);
                return comment is null ? Results.NotFound() : Results.Created($"/api/blog/comments/{comment.Id}", comment);
            })
            .WithValidation<CreateBlogCommentRequest>()
            .RequireRateLimiting(DependencyInjection.BlogCommentWriteRateLimitPolicy)
            .WithSummary("Comment on a published post")
            .WithDescription("Plain text, 10 to 3000 characters. The comment waits for an admin (status Pending) unless " +
                             "the author is one; the same text sent again by the same reader is BLOG_COMMENT_DUPLICATE. " +
                             "An unpublished post is 404.")
            .Produces<BlogCommentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status429TooManyRequests);

        userGroup.MapPost("/comments/{commentId:guid}/replies", async (Guid commentId, CreateBlogCommentRequest request, ClaimsPrincipal user,
                IBlogCommentService service, CancellationToken cancellationToken) =>
            {
                var comment = await service.ReplyAsync(user.GetUserId(), commentId, request, cancellationToken);
                return comment is null ? Results.NotFound() : Results.Created($"/api/blog/comments/{comment.Id}", comment);
            })
            .WithValidation<CreateBlogCommentRequest>()
            .RequireRateLimiting(DependencyInjection.BlogCommentWriteRateLimitPolicy)
            .WithSummary("Reply to a comment")
            .WithDescription("One level only: a reply to a reply answers that reply's root comment. The parent must be " +
                             "approved (404 otherwise). Same rules and moderation as a comment.")
            .Produces<BlogCommentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status429TooManyRequests);

        userGroup.MapPut("/comments/{commentId:guid}", async (Guid commentId, EditBlogCommentRequest request, ClaimsPrincipal user,
                IBlogCommentService service, IStringLocalizer<SharedStrings> localizer, CancellationToken cancellationToken) =>
            {
                try
                {
                    var comment = await service.EditAsync(user.GetUserId(), commentId, request, cancellationToken);
                    return comment is null ? Results.NotFound() : Results.Ok(comment);
                }
                catch (BlogCommentLockedException exception)
                {
                    // 409, not the 400 every other domain error gets: the form has to tell "you
                    // can't any more" apart from "fix the field".
                    return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: localizer[exception.ErrorCode]);
                }
            })
            .WithValidation<EditBlogCommentRequest>()
            .RequireRateLimiting(DependencyInjection.BlogCommentWriteRateLimitPolicy)
            .WithSummary("Edit your own comment while it waits for approval")
            .WithDescription("The author only — anyone else's comment is 404. Once approved or rejected the comment is " +
                             "locked: BLOG_COMMENT_LOCKED, 409.")
            .Produces<BlogCommentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

        userGroup.MapGet("/comments/mine", async ([AsParameters] MyBlogCommentListQuery query, ClaimsPrincipal user,
                IBlogCommentService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListMineAsync(user.GetUserId(), query, cancellationToken)))
            .WithValidation<MyBlogCommentListQuery>()
            .WithSummary("Your own comments, every status, newest first")
            .WithDescription("What the contributions page lists: each with the post it is on, its status, its helpful " +
                             "count and how many approved replies it has.")
            .Produces<PagedResult<MyBlogCommentResponse>>();

        userGroup.MapPost("/comments/{commentId:guid}/helpful", async (Guid commentId, ClaimsPrincipal user, IBlogCommentService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.ToggleHelpfulAsync(user.GetUserId(), commentId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireRateLimiting(DependencyInjection.BlogCommentHelpfulRateLimitPolicy)
            .WithSummary("Toggle \"helpful\" on an approved comment")
            .WithDescription("Idempotent per account: on, then off. A comment that is not on the site is 404.")
            .Produces<BlogCommentHelpfulResponse>()
            .Produces(StatusCodes.Status429TooManyRequests);

        userGroup.MapPost("/comments/{commentId:guid}/reports", async (Guid commentId, ReportBlogCommentRequest request, ClaimsPrincipal user,
                IBlogCommentService service, CancellationToken cancellationToken) =>
            {
                var result = await service.ReportAsync(user.GetUserId(), commentId, request, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithValidation<ReportBlogCommentRequest>()
            .RequireRateLimiting(DependencyInjection.BlogCommentReportRateLimitPolicy)
            .WithSummary("Report an approved comment")
            .WithDescription("One report per reader per comment: a second one answers with the first (alreadyReported). " +
                             "Reason \"Other\" needs a note. The report goes to the admins only.")
            .Produces<BlogCommentReportResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status429TooManyRequests);

        // ---- admin: moderation -----------------------------------------------------------------
        var adminGroup = app.MapGroup("/api/admin/blog/comments").WithTags("Admin").RequireAuthorization()
            .AddEndpointFilter<BlogEnabledFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        adminGroup.MapGet("", async ([AsParameters] AdminBlogCommentListQuery query, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogCommentService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await service.ListForAdminAsync(query, cancellationToken));
            })
            .WithValidation<AdminBlogCommentListQuery>()
            .WithSummary("Every comment, newest first, with optional status and reported filters")
            .WithDescription("Admin only. reported=true keeps comments with at least one open report. The author's " +
                             "address is included here and nowhere else.")
            .Produces<PagedResult<AdminBlogCommentListItemResponse>>();

        adminGroup.MapGet("/{commentId:guid}", async (Guid commentId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogCommentService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var comment = await service.GetForAdminAsync(commentId, cancellationToken);
                return comment is null ? Results.NotFound() : Results.Ok(comment);
            })
            .WithSummary("One comment with its reports and the root it answers")
            .Produces<AdminBlogCommentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPost("/{commentId:guid}/approve", async (Guid commentId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogCommentService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var comment = await service.ApproveAsync(user.GetUserId(), commentId, cancellationToken);
                return comment is null ? Results.NotFound() : Results.Ok(comment);
            })
            .WithSummary("Put a comment on the site")
            .Produces<AdminBlogCommentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPost("/{commentId:guid}/reject", async (Guid commentId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogCommentService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var comment = await service.RejectAsync(user.GetUserId(), commentId, cancellationToken);
                return comment is null ? Results.NotFound() : Results.Ok(comment);
            })
            .WithSummary("Take a comment off the site (or keep it off)")
            .WithDescription("Its replies go dark with it, and every open report on it is closed as action taken. " +
                             "The author is told only that it was not published.")
            .Produces<AdminBlogCommentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPost("/{commentId:guid}/reports/dismiss", async (Guid commentId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogCommentService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var comment = await service.DismissReportsAsync(user.GetUserId(), commentId, cancellationToken);
                return comment is null ? Results.NotFound() : Results.Ok(comment);
            })
            .WithSummary("Close a comment's open reports and keep the comment")
            .Produces<AdminBlogCommentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
