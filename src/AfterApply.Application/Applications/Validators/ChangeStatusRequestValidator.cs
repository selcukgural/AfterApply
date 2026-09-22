using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.Applications;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Applications.Validators;

public sealed class ChangeStatusRequestValidator : AbstractValidator<ChangeStatusRequest>
{
    public ChangeStatusRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.NewStatus).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);

        RuleFor(x => x.PromisedReplyBy).MustBeAReasonableReplyDate(localizer);
        // A promise is a reason to wait; a closing status leaves nothing to wait for.
        RuleFor(x => x.PromisedReplyBy)
            .Null()
            .When(x => TerminalApplicationStatuses.Values.Contains(x.NewStatus))
            .WithMessage(_ => localizer["VALIDATION_REPLY_PROMISE_ON_CLOSING_STATUS"]);

        RuleFor(x => x.RejectionNotice).IsInEnum();
        RuleFor(x => x.RejectionNotice)
            .Null()
            .When(x => x.NewStatus != ApplicationStatus.Rejected)
            .WithMessage(_ => localizer["VALIDATION_REJECTION_NOTICE_NOT_REJECTED"]);
    }
}
