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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AfterApply.Api.Endpoints;

/// <summary>
/// The writer's side of the blog. Same access rule as every admin surface: the Users.IsAdmin
/// column, re-read on every request, bare 403 otherwise. On top of that, a post that is not
/// published belongs to its author — every route here answers 404 for another admin's draft,
/// so one admin cannot tell whether another has one. Every write leaves a RequestAudit row like
/// any other; the autosave is not opted out (CLAUDE.md "Request audit").
/// </summary>
public static class AdminBlogEndpoints
{
    public static IEndpointRouteBuilder MapAdminBlogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/blog").WithTags("Admin").RequireAuthorization()
            .AddEndpointFilter<BlogEnabledFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/posts", async ([AsParameters] AdminBlogListQuery query, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await service.ListAsync(user.GetUserId(), query, cancellationToken));
            })
            .WithValidation<AdminBlogListQuery>()
            .WithSummary("The caller's drafts and every published post, most recently touched first")
            .WithDescription("Admin only. Other admins' unpublished posts are not listed. Optional status and lang filters.")
            .Produces<PagedResult<AdminBlogPostListItemResponse>>();

        group.MapGet("/posts/grouped", async ([AsParameters] AdminBlogGroupedListQuery query, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await service.ListGroupedAsync(user.GetUserId(), query, cancellationToken));
            })
            .WithValidation<AdminBlogGroupedListQuery>()
            .WithSummary("The admin table: one row per post and its translation, most recently touched pair first")
            .WithDescription("Admin only. A row holds the Turkish and the English post side by side, either null when " +
                             "there is none — or when it is another admin's draft, which the visible side's " +
                             "translationOfPostId still names. Paged by row, so a pair never straddles a page. The " +
                             "optional status filter keeps a row when either side matches and still shows both.")
            .Produces<PagedResult<AdminBlogPostGroupResponse>>();

        group.MapPost("/posts", async (CreateBlogPostRequest request, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var post = await service.CreateAsync(user.GetUserId(), request, cancellationToken);
                return Results.Created($"/api/admin/blog/posts/{post.Id}", post);
            })
            .WithValidation<CreateBlogPostRequest>()
            .WithSummary("Create a post from its first draft")
            .WithDescription("Admin only. The editor sends this once something has been typed; a request with no " +
                             "title, no summary and no text in the body is refused (BLOG_POST_EMPTY), so an editor " +
                             "opened and abandoned leaves no post. The draft is the caller's alone until it is " +
                             "published; later saves arrive through the draft autosave with the returned revision.")
            .Produces<AdminBlogPostResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/posts/{postId:guid}", async (Guid postId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var post = await service.GetAsync(user.GetUserId(), postId, cancellationToken);
                return post is null ? Results.NotFound() : Results.Ok(post);
            })
            .WithSummary("A post as the editor opens it: the draft, the published dates, the revision")
            .Produces<AdminBlogPostResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/posts/{postId:guid}/preview", async (Guid postId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var preview = await service.PreviewAsync(user.GetUserId(), postId, cancellationToken);
                return preview is null ? Results.NotFound() : Results.Ok(preview);
            })
            .WithSummary("The draft as the public page would show it")
            .WithDescription("Admin only; the author only while the post is unpublished. The draft slot in the public " +
                             "post's shape — same fields, same rules — so the web can render it with the public page's " +
                             "own component. The slug is the one publish would use; the dates are the ones publish " +
                             "would set. Reads only; nothing is published or saved.")
            .Produces<BlogPostPublicResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/posts/{postId:guid}/draft", async (Guid postId, SaveBlogDraftRequest request, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogAdminService service, IStringLocalizer<SharedStrings> localizer,
                CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                try
                {
                    var saved = await service.SaveDraftAsync(user.GetUserId(), postId, request, cancellationToken);
                    return saved is null ? Results.NotFound() : Results.Ok(saved);
                }
                catch (BlogPostRevisionConflictException exception)
                {
                    // 409, not the 400 every other domain error gets: the editor has to tell
                    // "reload, someone else saved" apart from "fix the field".
                    return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: localizer[exception.ErrorCode]);
                }
            })
            .WithValidation<SaveBlogDraftRequest>()
            .WithSummary("Save the draft (the editor's autosave)")
            .WithDescription("Admin only; the author only while the post is unpublished. The whole form travels every " +
                             "time. revision must be the one the editor last received — a mismatch is 409 and the " +
                             "editor reloads. Slug and language are refused with 400 once the post has been published. " +
                             "The HTML is sanitized here; what is stored is what will be rendered.")
            .Produces<BlogDraftSavedResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // The SEO suggestion (DECISIONS.md 2026-09-21): one model call over the draft's own text,
        // answered as proposals the editor applies field by field. Nothing is written here — an
        // author never finds their fields overwritten by a click.
        group.MapPost("/posts/{postId:guid}/seo-suggestions", async (Guid postId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var suggestion = await service.SuggestSeoAsync(user.GetUserId(), postId, cancellationToken);
                return suggestion is null ? Results.NotFound() : Results.Ok(suggestion);
            })
            .RequireRateLimiting(DependencyInjection.BlogSeoSuggestRateLimitPolicy)
            .WithSummary("Ask the model for the draft's SEO fields")
            .WithDescription("Proposals only — nothing is saved. seoTitle, metaDescription, primaryKeyword, " +
                             "secondaryKeywords, coverAlt (only with a cover), slug (only before the first publish) " +
                             "and a one-line intent note. 400 with a code when the model is not configured or answered nothing.")
            .Produces<BlogSeoSuggestionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/posts/{postId:guid}/publish", async (Guid postId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var post = await service.PublishAsync(user.GetUserId(), postId, cancellationToken);
                return post is null ? Results.NotFound() : Results.Ok(post);
            })
            .WithSummary("Publish the draft — first time or as an update to the live version")
            .WithDescription("Copies the draft over the published version and puts the post on the site. The slug is " +
                             "generated from the title on the first publish unless one was typed; a taken slug is 400.")
            .Produces<AdminBlogPostResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/posts/{postId:guid}/unpublish", async (Guid postId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var post = await service.UnpublishAsync(user.GetUserId(), postId, cancellationToken);
                return post is null ? Results.NotFound() : Results.Ok(post);
            })
            .WithSummary("Take a post off the site")
            .WithDescription("The published version and the slug are kept, so publishing again restores the same URL.")
            .Produces<AdminBlogPostResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/posts/{postId:guid}", async (Guid postId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return await service.DeleteAsync(user.GetUserId(), postId, cancellationToken) ? Results.NoContent() : Results.NotFound();
            })
            .WithSummary("Delete a post outright")
            .WithDescription("Permanent — its likes, its media rows and its stored images go with it.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/posts/{postId:guid}/media", async (Guid postId, [FromForm] IFormFile file, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IBlogMediaService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                try
                {
                    await using var stream = file.OpenReadStream();
                    var created = await service.UploadAsync(user.GetUserId(), postId, stream, file.Length, cancellationToken);
                    return created is null ? Results.NotFound() : Results.Created(created.Url, created);
                }
                catch (BlogUploadValidationException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [exception.Field] = exception.Errors.ToArray()
                    });
                }
            })
            .DisableAntiforgery()
            .RequireRateLimiting(DependencyInjection.UploadRateLimitPolicy)
            .WithSummary("Upload an image into a post")
            .WithDescription("multipart/form-data with a 'file' part. PNG, JPEG, GIF or WebP by the file's own leading " +
                             "bytes — the name and the declared type are not consulted; at most Storage:MaxFileSizeBytes. " +
                             "The response's url is what the editor puts in the image's src.")
            .Produces<BlogMediaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
