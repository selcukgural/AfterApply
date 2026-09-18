using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.CompanySalaries;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.CompanySalaries.Validators;

public sealed class CompanySalaryRequestValidator : AbstractValidator<CompanySalaryRequest>
{
    public CompanySalaryRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        // The occupation comes from the catalogue; the service checks the id exists. Here only that
        // one was sent at all.
        RuleFor(x => x.OccupationId).NotEmpty()
            .WithMessage(_ => localizer["COMPANY_SALARY_OCCUPATION_UNKNOWN"]);

        RuleFor(x => x.YearsOfExperience)
            .InclusiveBetween(CompanySalaryEntry.MinYearsOfExperience, CompanySalaryEntry.MaxYearsOfExperience);

        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.EmploymentStatus).IsInEnum();
        RuleFor(x => x.Currency).IsInEnum();

        RuleFor(x => x.MonthlyNetAmount)
            .InclusiveBetween(CompanySalaryEntry.MinAmount, CompanySalaryEntry.MaxAmount);

        // "Bonus?" is a question with two answers, and the amount belongs to exactly one of them.
        RuleFor(x => x.AnnualBonusAmount).NotNull()
            .When(x => x.HasBonus)
            .WithMessage(_ => localizer["VALIDATION_SALARY_BONUS_AMOUNT_REQUIRED"]);
        RuleFor(x => x.AnnualBonusAmount).Null()
            .When(x => !x.HasBonus)
            .WithMessage(_ => localizer["VALIDATION_SALARY_BONUS_AMOUNT_UNEXPECTED"]);
        RuleFor(x => x.AnnualBonusAmount!.Value)
            .InclusiveBetween(CompanySalaryEntry.MinAmount, CompanySalaryEntry.MaxAmount)
            .When(x => x.HasBonus && x.AnnualBonusAmount.HasValue)
            .OverridePropertyName(nameof(CompanySalaryRequest.AnnualBonusAmount));
    }

}

public sealed class CompanySalaryListQueryValidator : AbstractValidator<CompanySalaryListQuery>
{
    public CompanySalaryListQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
    }
}

public sealed class AdminCompanySalaryListQueryValidator : AbstractValidator<AdminCompanySalaryListQuery>
{
    public AdminCompanySalaryListQueryValidator()
    {
        RuleFor(x => x.Company).MaximumLength(100);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
    }
}
