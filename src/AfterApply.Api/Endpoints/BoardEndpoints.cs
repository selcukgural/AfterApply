using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Api.Filters;
using AfterApply.Application.Board;
using AfterApply.Application.Board.Contracts;
using AfterApply.Domain.Board;
using AfterApply.Infrastructure.Identity;

namespace AfterApply.Api.Endpoints;

public static class BoardEndpoints
{
    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/board").WithTags("Board").RequireAuthorization()
            .AddEndpointFilter<BoardEnabledFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/", async ([AsParameters] BoardFilterQuery query, ClaimsPrincipal user, IBoardService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetBoardAsync(user.GetUserId(), query, cancellationToken)))
            .WithValidation<BoardFilterQuery>()
            .WithSummary("Read the current user's applications board")
            .WithDescription("The first page (Limit cards, 10 by default) of each of the five columns, each with its total " +
                             "under the filter and a cursor for the next page. The first call for a user fills the board " +
                             "once with open applications and saved postings active in the last Board:SeedWindowDays days. " +
                             "Filters narrow the response only; they never change what is on the board.")
            .Produces<BoardResponse>();

        group.MapGet("/columns/{column}", async (BoardColumn column, [AsParameters] BoardColumnQuery query,
                ClaimsPrincipal user, IBoardService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetColumnAsync(user.GetUserId(), column, query, cancellationToken)))
            .WithValidation<BoardColumnQuery>()
            .WithSummary("Read the next page of one board column")
            .WithDescription("Keyset-paged after Cursor (the previous page's NextCursor), in the column's own order, so " +
                             "cards arriving on top while the user scrolls do not repeat or skip one.")
            .Produces<BoardColumnPage>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/cards", async (AddBoardCardsRequest request, ClaimsPrincipal user, IBoardService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.AddAsync(user.GetUserId(), request, cancellationToken)))
            .WithValidation<AddBoardCardsRequest>()
            .WithSummary("Put applications or saved postings on the board")
            .WithDescription("Each lands on top of its own column. Ids that are not the caller's, or already on the board, " +
                             "are skipped; Added counts the cards actually made.")
            .Produces<AddBoardCardsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("/cards/{id:guid}", async (Guid id, ClaimsPrincipal user, IBoardService service,
                CancellationToken cancellationToken) =>
                await service.RemoveAsync(user.GetUserId(), id, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithSummary("Take a card off the board")
            .WithDescription("The application or saved posting itself is not touched.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/cards/{id:guid}/move", async (Guid id, MoveBoardCardRequest request, ClaimsPrincipal user,
                IBoardService service, CancellationToken cancellationToken) =>
                await service.MoveAsync(user.GetUserId(), id, request, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithValidation<MoveBoardCardRequest>()
            .WithSummary("Move or reorder a card")
            .WithDescription("A ToStatus different from the application's changes its status exactly as the status endpoint " +
                             "does (manual origin, history row, reminders); a saved posting given a status becomes an " +
                             "application first. The card then lands directly under AboveCardId, else over BelowCardId, " +
                             "else on top of its column.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/cards/seen", async (MarkBoardCardsSeenRequest request, ClaimsPrincipal user, IBoardService service,
                CancellationToken cancellationToken) =>
            {
                await service.MarkSeenAsync(user.GetUserId(), request, cancellationToken);
                return Results.NoContent();
            })
            .WithValidation<MarkBoardCardsSeenRequest>()
            .WithSummary("Clear the \"arrived without you\" mark")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
