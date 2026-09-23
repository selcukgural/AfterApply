using AfterApply.Application.Notifications.Contracts;
using FluentValidation;

namespace AfterApply.Application.Notifications.Validators;

public sealed class GetNotificationFeedQueryValidator : AbstractValidator<GetNotificationFeedQuery>
{
    public GetNotificationFeedQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
