using AfterApply.Application.Board.Contracts;
using FluentValidation;

namespace AfterApply.Application.Board.Validators;

public static class BoardLimits
{
    /// <summary>Largest page a column read may ask for. The board asks for 10; the ceiling only
    /// stops a hand-made request from turning one column read into a whole-account dump.</summary>
    public const int MaxPageSize = 50;

    /// <summary>Most ids one "add to board" request may carry — a screenful of ticked rows, with
    /// room to spare.</summary>
    public const int MaxAddPerRequest = 100;

    /// <summary>Most ids one "mark seen" request may carry.</summary>
    public const int MaxSeenPerRequest = 200;

    public const int MaxSearchLength = 200;
}

public sealed class BoardFilterQueryValidator : AbstractValidator<BoardFilterQuery>
{
    public BoardFilterQueryValidator()
    {
        Include(new BoardFilterRules<BoardFilterQuery>());
    }
}

public sealed class BoardColumnQueryValidator : AbstractValidator<BoardColumnQuery>
{
    public BoardColumnQueryValidator()
    {
        Include(new BoardFilterRules<BoardColumnQuery>());
        RuleFor(x => x.Cursor)
            .Must(cursor => BoardCursor.TryDecode(cursor, out _))
            .When(x => x.Cursor is not null)
            .WithErrorCode("BOARD_CURSOR_INVALID");
    }
}

internal sealed class BoardFilterRules<T> : AbstractValidator<T> where T : BoardFilterQuery
{
    public BoardFilterRules()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, BoardLimits.MaxPageSize).When(x => x.Limit is not null);
        RuleFor(x => x.Search).MaximumLength(BoardLimits.MaxSearchLength);
        RuleForEach(x => x.Sources).IsInEnum();
        RuleFor(x => x.ActiveTo)
            .GreaterThanOrEqualTo(x => x.ActiveFrom)
            .When(x => x.ActiveFrom is not null && x.ActiveTo is not null);
    }
}

public sealed class AddBoardCardsRequestValidator : AbstractValidator<AddBoardCardsRequest>
{
    public AddBoardCardsRequestValidator()
    {
        RuleFor(x => (x.ApplicationIds == null ? 0 : x.ApplicationIds.Count) + (x.TrackedJobIds == null ? 0 : x.TrackedJobIds.Count))
            .InclusiveBetween(1, BoardLimits.MaxAddPerRequest)
            .OverridePropertyName("ApplicationIds");
        RuleForEach(x => x.ApplicationIds).NotEqual(Guid.Empty);
        RuleForEach(x => x.TrackedJobIds).NotEqual(Guid.Empty);
    }
}

public sealed class MoveBoardCardRequestValidator : AbstractValidator<MoveBoardCardRequest>
{
    public MoveBoardCardRequestValidator()
    {
        RuleFor(x => x.ToStatus).IsInEnum().When(x => x.ToStatus is not null);
        RuleFor(x => x.AboveCardId).NotEqual(Guid.Empty).When(x => x.AboveCardId is not null);
        RuleFor(x => x.BelowCardId).NotEqual(Guid.Empty).When(x => x.BelowCardId is not null);
        RuleFor(x => x.BelowCardId)
            .NotEqual(x => x.AboveCardId)
            .When(x => x.AboveCardId is not null && x.BelowCardId is not null);
    }
}

public sealed class MarkBoardCardsSeenRequestValidator : AbstractValidator<MarkBoardCardsSeenRequest>
{
    public MarkBoardCardsSeenRequestValidator()
    {
        RuleFor(x => x.CardIds)
            .NotEmpty()
            .When(x => !x.All)
            .WithMessage("Either list the cards or set All.");
        RuleFor(x => x.CardIds!.Count)
            .LessThanOrEqualTo(BoardLimits.MaxSeenPerRequest)
            .When(x => x.CardIds is not null)
            .OverridePropertyName("CardIds");
        RuleForEach(x => x.CardIds).NotEqual(Guid.Empty);
    }
}
