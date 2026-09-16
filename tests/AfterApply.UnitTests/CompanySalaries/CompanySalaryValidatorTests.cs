using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.CompanySalaries.Validators;
using AfterApply.Application.Localization;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanySalaries;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.CompanySalaries;

public class CompanySalaryValidatorTests
{
    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private static CompanySalaryRequestValidator Validator() => new(new KeyEchoLocalizer());

    private static readonly Guid Backend = Guid.NewGuid();

    private static CompanySalaryRequest Request(Guid? occupation = null, int years = 6,
        decimal amount = 95_000m, bool hasBonus = true, decimal? bonus = 120_000m,
        EmploymentType type = EmploymentType.FullTime, SalaryEmploymentStatus status = SalaryEmploymentStatus.CurrentEmployee,
        SalaryCurrency currency = SalaryCurrency.TRY) =>
        new(occupation ?? Backend, years, type, status, amount, currency, hasBonus, bonus);

    [Fact]
    public void Accepts_A_Complete_Entry()
    {
        Validator().Validate(Request()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Accepts_No_Bonus_With_No_Amount()
    {
        Validator().Validate(Request(hasBonus: false, bonus: null)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_A_Missing_Occupation()
    {
        Validator().Validate(Request(Guid.Empty))
            .Errors.ShouldContain(e => e.PropertyName == "OccupationId" && e.ErrorMessage == "COMPANY_SALARY_OCCUPATION_UNKNOWN");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    public void Rejects_Years_Off_The_Scale(int years)
    {
        Validator().Validate(Request(years: years)).Errors.ShouldContain(e => e.PropertyName == "YearsOfExperience");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_000_001)]
    public void Rejects_An_Amount_Off_The_Scale(decimal amount)
    {
        Validator().Validate(Request(amount: amount)).Errors.ShouldContain(e => e.PropertyName == "MonthlyNetAmount");
    }

    [Fact]
    public void Bonus_Yes_Needs_An_Amount()
    {
        Validator().Validate(Request(hasBonus: true, bonus: null))
            .Errors.ShouldContain(e => e.PropertyName == "AnnualBonusAmount" && e.ErrorMessage == "VALIDATION_SALARY_BONUS_AMOUNT_REQUIRED");
    }

    [Fact]
    public void Bonus_No_Refuses_An_Amount()
    {
        Validator().Validate(Request(hasBonus: false, bonus: 1_000m))
            .Errors.ShouldContain(e => e.PropertyName == "AnnualBonusAmount" && e.ErrorMessage == "VALIDATION_SALARY_BONUS_AMOUNT_UNEXPECTED");
    }

    [Fact]
    public void Bonus_Amount_Is_Held_To_The_Same_Range()
    {
        Validator().Validate(Request(hasBonus: true, bonus: 0m)).Errors.ShouldContain(e => e.PropertyName == "AnnualBonusAmount");
    }

    [Fact]
    public void Rejects_Undefined_Enum_Values()
    {
        var result = Validator().Validate(Request(type: (EmploymentType)99, status: (SalaryEmploymentStatus)99, currency: (SalaryCurrency)99));

        result.Errors.ShouldContain(e => e.PropertyName == "EmploymentType");
        result.Errors.ShouldContain(e => e.PropertyName == "EmploymentStatus");
        result.Errors.ShouldContain(e => e.PropertyName == "Currency");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void List_Query_Page_Is_Bounded(int page)
    {
        new CompanySalaryListQueryValidator().Validate(new CompanySalaryListQuery(page)).IsValid.ShouldBeFalse();
    }
}
