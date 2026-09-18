using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.CompanySalaries;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.CompanySalaries.Validators;

public sealed class CompanySalaryRequestValidator : AbstractValidator<CompanySalaryRequest>
{
    public CompanySalaryRequestValidator(IStringLocalizer<SharedStrings> localizer, TimeProvider? timeProvider = null)
    {
        // The clock is optional the way the services take it: tests pin a year, the host uses
        // the system one. Read at construction, which is once per request (validators are
        // scoped), so one request straddling New Year gets one answer.
        var currentYear = (timeProvider ?? TimeProvider.System).GetUtcNow().Year;

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

        // The period: the shape allows null so older clients still parse, the rule does not —
        // every write since 2026-09-18 says which years the salary belongs to.
        RuleFor(x => x.PeriodStartYear).NotNull()
            .WithMessage(_ => localizer["VALIDATION_SALARY_PERIOD_START_REQUIRED"]);
        RuleFor(x => x.PeriodStartYear!.Value)
            .InclusiveBetween(CompanySalaryEntry.MinPeriodYear, currentYear)
            .When(x => x.PeriodStartYear.HasValue)
            .WithMessage(_ => localizer["VALIDATION_SALARY_PERIOD_YEAR_OUT_OF_RANGE", CompanySalaryEntry.MinPeriodYear, currentYear])
            .OverridePropertyName(nameof(CompanySalaryRequest.PeriodStartYear));

        // A current employee is still drawing it (no end year); a former one must say when it ended.
        RuleFor(x => x.PeriodEndYear).Null()
            .When(x => x.EmploymentStatus == SalaryEmploymentStatus.CurrentEmployee)
            .WithMessage(_ => localizer["VALIDATION_SALARY_PERIOD_END_UNEXPECTED"]);
        RuleFor(x => x.PeriodEndYear).NotNull()
            .When(x => x.EmploymentStatus == SalaryEmploymentStatus.FormerEmployee)
            .WithMessage(_ => localizer["VALIDATION_SALARY_PERIOD_END_REQUIRED"]);
        RuleFor(x => x.PeriodEndYear!.Value)
            .InclusiveBetween(CompanySalaryEntry.MinPeriodYear, currentYear)
            .When(x => x.EmploymentStatus == SalaryEmploymentStatus.FormerEmployee && x.PeriodEndYear.HasValue)
            .WithMessage(_ => localizer["VALIDATION_SALARY_PERIOD_YEAR_OUT_OF_RANGE", CompanySalaryEntry.MinPeriodYear, currentYear])
            .OverridePropertyName(nameof(CompanySalaryRequest.PeriodEndYear));
        RuleFor(x => x.PeriodEndYear!.Value)
            .GreaterThanOrEqualTo(x => x.PeriodStartYear!.Value)
            .When(x => x.EmploymentStatus == SalaryEmploymentStatus.FormerEmployee && x.PeriodEndYear.HasValue && x.PeriodStartYear.HasValue)
            .WithMessage(_ => localizer["VALIDATION_SALARY_PERIOD_END_BEFORE_START"])
            .OverridePropertyName(nameof(CompanySalaryRequest.PeriodEndYear));
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
