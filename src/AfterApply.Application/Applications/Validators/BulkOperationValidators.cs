using AfterApply.Application.Applications.Contracts;
using FluentValidation;

namespace AfterApply.Application.Applications.Validators;

/// <summary>
/// The shape rules every bulk request shares: exactly one selection form, a non-empty id list when
/// that is the form, and an ExpectedCount whenever the server — not the user — is the one resolving
/// which rows are in scope. The *size* ceiling is not here on purpose: it lives in configuration
/// (ApplicationBulkOptions) and is enforced in the service, so the number the user is told is the
/// number the deployment actually runs with.
/// </summary>
internal static class BulkSelectionRules
{
    internal static IRuleBuilderOptions<T, BulkSelection> ValidSelection<T>(this IRuleBuilder<T, BulkSelection> rule) =>
        rule.NotNull()
            .Must(selection => selection.Ids is not null ^ selection.AllMatching is not null)
            .WithMessage("Provide either an explicit id list or an all-matching filter, not both.")
            .Must(selection => selection.Ids is null || selection.Ids.Count > 0)
            .WithMessage("The id list cannot be empty.")
            .Must(selection => selection.Ids is null || selection.Ids.Distinct().Count() == selection.Ids.Count)
            .WithMessage("The id list cannot repeat an application.");

    /// <summary>An all-matching selection is resolved server-side, so the caller has to say how many
    /// rows it believed were in scope; an explicit id list is already the exact set and needs no
    /// second opinion.</summary>
    internal static bool HasCountWhenNeeded(BulkSelection selection, int? expectedCount) =>
        selection.AllMatching is null || expectedCount is >= 0;
}

public sealed class BulkChangeStatusRequestValidator : AbstractValidator<BulkChangeStatusRequest>
{
    public BulkChangeStatusRequestValidator()
    {
        RuleFor(x => x.Selection).ValidSelection();
        RuleFor(x => x.NewStatus).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.ExpectedCount)
            .Must((request, expectedCount) => BulkSelectionRules.HasCountWhenNeeded(request.Selection, expectedCount))
            .When(x => x.Selection is not null)
            .WithMessage("An all-matching selection must state the count it was shown.");
    }
}

public sealed class BulkDeleteRequestValidator : AbstractValidator<BulkDeleteRequest>
{
    public BulkDeleteRequestValidator()
    {
        RuleFor(x => x.Selection).ValidSelection();
        RuleFor(x => x.ExpectedCount)
            .Must((request, expectedCount) => BulkSelectionRules.HasCountWhenNeeded(request.Selection, expectedCount))
            .When(x => x.Selection is not null)
            .WithMessage("An all-matching selection must state the count it was shown.");
    }
}

public sealed class UndoBulkStatusRequestValidator : AbstractValidator<UndoBulkStatusRequest>
{
    public UndoBulkStatusRequestValidator()
    {
        RuleFor(x => x.Entries).NotNull().NotEmpty();
        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.ApplicationId).NotEmpty();
            entry.RuleFor(e => e.ExpectedStatus).IsInEnum();
            entry.RuleFor(e => e.RevertTo).IsInEnum();
            // A no-op entry is a client bug, not a user intent — and letting it through would make
            // the domain throw ApplicationAlreadyInStatus mid-batch.
            entry.RuleFor(e => e.RevertTo).NotEqual(e => e.ExpectedStatus)
                .WithMessage("An undo entry must move the application somewhere else.");
        });
    }
}
