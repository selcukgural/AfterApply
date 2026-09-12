using AfterApply.Application.JobSearch.Contracts;
using FluentValidation;

namespace AfterApply.Application.JobSearch.Validators;

public sealed class UpdateJobSearchPreferencesRequestValidator : AbstractValidator<UpdateJobSearchPreferencesRequest>
{
    public UpdateJobSearchPreferencesRequestValidator()
    {
        RuleFor(x => x.DefaultCountry).MustBeCountryCode();
        RuleFor(x => x.DefaultLanguage).MustBeLanguageCode();
        RuleFor(x => x.DefaultLocation).MaximumLength(JobSearchValidationRules.MaxLocationLength);
        RuleFor(x => x.DefaultDatePosted).IsInEnum().When(x => x.DefaultDatePosted is not null);
    }
}
