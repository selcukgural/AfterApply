using AfterApply.Application.Identity;

namespace AfterApply.Api.Endpoints;

public static class AvatarEndpoints
{
    public static IEndpointRouteBuilder MapAvatarEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous: the web app draws the photo with a plain <img>, which sends no token, and a
        // reader of a blog comment is often signed out. What keeps it narrow is the address — a
        // random id that is not the user id, handed only to the owner and (when they chose so) next
        // to their blog comments, and replaced on every change (DECISIONS.md 2026-09-28).
        //
        // A day's cache, not the blog images' year: the id changes on every upload, so a new photo
        // shows at once, but an admin's removal should not outlive a browser cache for long.
        // Served inline; the stored bytes are always a WebP the server encoded itself, and the API's
        // nosniff header and default-src 'none' CSP cover the rest.
        app.MapGet("/api/avatars/{publicId:guid}", async (Guid publicId, IAvatarService avatarService,
                HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                var avatar = await avatarService.OpenAsync(publicId, cancellationToken);
                if (avatar is null)
                {
                    return Results.NotFound();
                }

                httpContext.Response.Headers.CacheControl = "public, max-age=86400";
                return Results.Stream(avatar.Content, avatar.ContentType);
            })
            .WithTags("Users")
            .AllowAnonymous()
            .WithSummary("A profile photo")
            .WithDescription("Public, by the random id in a profile's or a blog comment's avatarUrl. 404 once the " +
                             "photo is replaced or removed, or hidden from comments.")
            .Produces<IResult>(StatusCodes.Status200OK, "image/webp")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
