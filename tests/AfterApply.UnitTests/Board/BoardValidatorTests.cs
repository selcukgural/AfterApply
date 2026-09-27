using AfterApply.Application.Board;
using AfterApply.Application.Board.Contracts;
using AfterApply.Application.Board.Validators;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using Shouldly;

namespace AfterApply.UnitTests.Board;

public class BoardCursorTests
{
    [Fact]
    public void A_Cursor_Round_Trips()
    {
        var cursor = new BoardCursor(-3 * (1L << 20), Guid.CreateVersion7());

        BoardCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();
        decoded.ShouldBe(cursor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("MTIz")] // "123": no separator
    [InlineData("YWJjOmRlZg")] // "abc:def"
    public void Anything_Not_Made_By_Encode_Is_Refused(string value)
    {
        BoardCursor.TryDecode(value, out _).ShouldBeFalse();
    }

    [Fact]
    public void An_Oversized_Cursor_Is_Refused_Before_Decoding()
    {
        BoardCursor.TryDecode(new string('A', 500), out _).ShouldBeFalse();
    }
}

public class BoardValidatorTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(BoardLimits.MaxPageSize, true)]
    [InlineData(BoardLimits.MaxPageSize + 1, false)]
    public void The_Page_Size_Is_Bounded(int limit, bool valid)
    {
        new BoardFilterQueryValidator().Validate(new BoardFilterQuery { Limit = limit }).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void An_Unknown_Source_Is_Refused()
    {
        new BoardFilterQueryValidator().Validate(new BoardFilterQuery { Sources = [(Source)999] }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void An_Activity_Range_Cannot_End_Before_It_Starts()
    {
        var now = DateTimeOffset.UtcNow;
        new BoardFilterQueryValidator()
            .Validate(new BoardFilterQuery { ActiveFrom = now, ActiveTo = now.AddDays(-1) })
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_Tampered_Cursor_Is_Refused()
    {
        new BoardColumnQueryValidator().Validate(new BoardColumnQuery { Cursor = "garbage" }).IsValid.ShouldBeFalse();
        new BoardColumnQueryValidator()
            .Validate(new BoardColumnQuery { Cursor = new BoardCursor(0, Guid.NewGuid()).Encode() })
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void An_Add_Request_Needs_At_Least_One_Id_And_At_Most_The_Cap()
    {
        var validator = new AddBoardCardsRequestValidator();

        validator.Validate(new AddBoardCardsRequest(null, null)).IsValid.ShouldBeFalse();
        validator.Validate(new AddBoardCardsRequest([Guid.NewGuid()], null)).IsValid.ShouldBeTrue();
        validator.Validate(new AddBoardCardsRequest(
                Enumerable.Range(0, BoardLimits.MaxAddPerRequest).Select(_ => Guid.NewGuid()).ToList(), [Guid.NewGuid()]))
            .IsValid.ShouldBeFalse();
        validator.Validate(new AddBoardCardsRequest([Guid.Empty], null)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_Move_Cannot_Name_The_Same_Neighbour_Twice()
    {
        var id = Guid.NewGuid();
        new MoveBoardCardRequestValidator().Validate(new MoveBoardCardRequest(null, id, id)).IsValid.ShouldBeFalse();
        new MoveBoardCardRequestValidator()
            .Validate(new MoveBoardCardRequest(ApplicationStatus.Interview, id, Guid.NewGuid())).IsValid.ShouldBeTrue();
        new MoveBoardCardRequestValidator()
            .Validate(new MoveBoardCardRequest((ApplicationStatus)999, null, null)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Mark_Seen_Needs_Ids_Unless_It_Is_All()
    {
        var validator = new MarkBoardCardsSeenRequestValidator();

        validator.Validate(new MarkBoardCardsSeenRequest(null, All: false)).IsValid.ShouldBeFalse();
        validator.Validate(new MarkBoardCardsSeenRequest(null, All: true)).IsValid.ShouldBeTrue();
        validator.Validate(new MarkBoardCardsSeenRequest([Guid.NewGuid()], All: false)).IsValid.ShouldBeTrue();
    }
}
