using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Applications.Validators;
using AfterApply.Domain.Applications;
using Shouldly;

namespace AfterApply.UnitTests.Applications;

public class BulkOperationValidatorTests
{
    private static readonly Guid FirstId = Guid.NewGuid();
    private static readonly Guid SecondId = Guid.NewGuid();

    private static BulkDeleteRequest Delete(BulkSelection selection, int? expectedCount = null) =>
        new(selection, expectedCount);

    [Fact]
    public void An_Explicit_Id_List_Needs_No_Expected_Count()
    {
        // The set is already exact — asking the caller to count its own array would only reject
        // legitimate requests whose rows vanished in another tab.
        var result = new BulkDeleteRequestValidator().Validate(Delete(new BulkSelection(Ids: [FirstId])));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void An_All_Matching_Selection_Must_State_The_Count_It_Was_Shown()
    {
        var result = new BulkDeleteRequestValidator()
            .Validate(Delete(new BulkSelection(AllMatching: new BulkFilterSelection())));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(BulkDeleteRequest.ExpectedCount));
    }

    [Fact]
    public void An_All_Matching_Selection_With_A_Count_Is_Accepted()
    {
        var result = new BulkDeleteRequestValidator()
            .Validate(Delete(new BulkSelection(AllMatching: new BulkFilterSelection(Status: ApplicationStatus.Rejected)), 12));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_Zero_Count_Is_A_Legitimate_Expectation()
    {
        // Not a missing value: "the filter matched nothing when I looked" is a claim the server can
        // check, and it is exactly the claim that catches a row arriving in the meantime.
        var result = new BulkDeleteRequestValidator()
            .Validate(Delete(new BulkSelection(AllMatching: new BulkFilterSelection()), 0));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Naming_Both_Selection_Forms_Is_Rejected()
    {
        var result = new BulkDeleteRequestValidator()
            .Validate(Delete(new BulkSelection(Ids: [FirstId], AllMatching: new BulkFilterSelection()), 1));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Naming_Neither_Selection_Form_Is_Rejected()
    {
        var result = new BulkDeleteRequestValidator().Validate(Delete(new BulkSelection()));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void An_Empty_Id_List_Is_Rejected()
    {
        var result = new BulkDeleteRequestValidator().Validate(Delete(new BulkSelection(Ids: [])));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_Repeated_Id_Is_Rejected()
    {
        var result = new BulkDeleteRequestValidator().Validate(Delete(new BulkSelection(Ids: [FirstId, FirstId])));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_Status_Change_Note_Is_Capped_At_The_Column_Length()
    {
        var result = new BulkChangeStatusRequestValidator().Validate(new BulkChangeStatusRequest(
            new BulkSelection(Ids: [FirstId]), ApplicationStatus.Rejected, new string('x', 501)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(BulkChangeStatusRequest.Note));
    }

    [Fact]
    public void A_Status_Change_Must_Name_A_Real_Status()
    {
        var result = new BulkChangeStatusRequestValidator().Validate(new BulkChangeStatusRequest(
            new BulkSelection(Ids: [FirstId]), (ApplicationStatus)999));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void An_Undo_Entry_That_Moves_Nowhere_Is_Rejected()
    {
        // A no-op entry is a client bug, and letting it through would make the domain throw
        // ApplicationAlreadyInStatus part-way through a batch.
        var result = new UndoBulkStatusRequestValidator().Validate(new UndoBulkStatusRequest(
            [new UndoBulkStatusEntry(FirstId, ApplicationStatus.Rejected, ApplicationStatus.Rejected)]));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void An_Undo_With_No_Entries_Is_Rejected()
    {
        var result = new UndoBulkStatusRequestValidator().Validate(new UndoBulkStatusRequest([]));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_Well_Formed_Undo_Is_Accepted()
    {
        var result = new UndoBulkStatusRequestValidator().Validate(new UndoBulkStatusRequest(
        [
            new UndoBulkStatusEntry(FirstId, ApplicationStatus.Rejected, ApplicationStatus.Applied),
            new UndoBulkStatusEntry(SecondId, ApplicationStatus.Rejected, ApplicationStatus.Interview)
        ]));

        result.IsValid.ShouldBeTrue();
    }
}
