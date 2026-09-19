using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Api.Filters;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Blog;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Infrastructure;

namespace AfterApply.Api.Endpoints;

/// <summary>The reader's side of the blog (DECISIONS.md 2026-09-19): the public list, the post,
/// the sitemap feed, the images, and the one thing a signed-in reader can do — like.</summary>
public static class BlogEndpoints
{
    public static IEndpointRouteBuilder MapBlogEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous, and that is the point: a post is something a stranger reads from a search
        // result. No RequireAuthorization means a stale bearer token a signed-in browser attaches
        // is ignored rather than answered with a 401 — so the web app's refresh loop never fires
        // on a public page. Every query behind these routes starts from Status == Published
        // (BlogPublicService); a draft has no path to this surface.
        var publicGroup = app.MapGroup("/api/blog/public").WithTags("Blog")
            .AddEndpointFilter<BlogEnabledFilter>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        publicGroup.MapGet("/posts", async ([AsParameters] PublicBlogListQuery query, IBlogPublicService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(query, cancellationToken)))
            .WithValidation<PublicBlogListQuery>()
            .WithSummary("Published posts in one language, newest first")
            .WithDescription("Public. lang is tr or en; a post lives in exactly one language. Only the published " +
                             "version of a post is ever on the wire — never the draft being edited.")
            .Produces<PagedResult<BlogPostListItemResponse>>();

        publicGroup.MapGet("/slugs", async (IBlogPublicService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListSlugsAsync(cancellationToken)))
            .WithSummary("Every published post's language, slug and dates, for the sitemap")
            .Produces<IReadOnlyList<BlogSlugResponse>>();

        publicGroup.MapGet("/posts/{lang}/{slug}", async (string lang, string slug, ClaimsPrincipal user,
                IBlogPublicService service, CancellationToken cancellationToken) =>
            {
                var post = await service.GetBySlugAsync(lang, slug, user.TryGetUserId(), cancellationToken);
                return post is null ? Results.NotFound() : Results.Ok(post);
            })
            .WithSummary("A published post")
            .WithDescription("Public. contentHtml is the sanitized published version, rendered as-is by the web " +
                             "app. likedByMe is null for an anonymous reader and a boolean when the request " +
                             "carried a valid token — the token is optional, and a stale one is ignored.")
            .Produces<BlogPostPublicResponse>();

        // Images are served inline — a post's pictures are meant to be looked at — which is why
        // the upload only ever accepts PNG/JPEG/GIF/WebP by their bytes and never SVG (a script
        // host). The API's nosniff header and default-src 'none' CSP cover the rest.
        app.MapGet("/api/blog/media/{id:guid}", async (Guid id, ClaimsPrincipal user, IBlogMediaService service,
                HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                var media = await service.OpenAsync(id, user.TryGetUserId(), cancellationToken);
                if (media is null)
                {
                    return Results.NotFound();
                }

                // A published post's image is immutable content at an id-only address: let every
                // cache keep it for a year. The author's own draft image is theirs alone.
                httpContext.Response.Headers.CacheControl = media.IsPublic
                    ? "public, max-age=31536000, immutable"
                    : "private, no-store";

                return Results.Stream(media.Content, media.ContentType);
            })
            .WithTags("Blog")
            .AddEndpointFilter<BlogEnabledFilter>()
            .WithSummary("A blog image")
            .WithDescription("Inline. Public once the post is published; until then only the post's author " +
                             "(by token) can load it, and everyone else — other admins included — gets 404.")
            .Produces<IResult>(StatusCodes.Status200OK, "image/png")
            .ProducesProblem(StatusCodes.Status404NotFound);

        var userGroup = app.MapGroup("/api/blog").WithTags("Blog").RequireAuthorization()
            .AddEndpointFilter<BlogEnabledFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        userGroup.MapPost("/posts/{postId:guid}/like", async (Guid postId, ClaimsPrincipal user, IBlogPublicService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.ToggleLikeAsync(user.GetUserId(), postId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireRateLimiting(DependencyInjection.BlogLikeRateLimitPolicy)
            .WithSummary("Toggle a like on a published post")
            .WithDescription("Idempotent per account: on, then off. A post that is not published is 404.")
            .Produces<BlogLikeToggleResponse>()
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
