using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Applications.Validators;

public sealed class SetReplyPromiseRequestValidator : AbstractValidator<SetReplyPromiseRequest>
{
    public SetReplyPromiseRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.PromisedReplyBy).MustBeAReasonableReplyDate(localizer);
    }
}
