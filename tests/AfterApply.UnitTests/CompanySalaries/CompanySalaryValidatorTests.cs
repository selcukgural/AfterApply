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

    // Pinned so "this year" in the rules is 2026 whatever the calendar says when the suite runs.
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static CompanySalaryRequestValidator Validator() => new(new KeyEchoLocalizer(), new FixedClock(Now));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly Guid Backend = Guid.NewGuid();

    private static CompanySalaryRequest Request(Guid? occupation = null, int years = 6,
        decimal amount = 95_000m, bool hasBonus = true, decimal? bonus = 120_000m,
        EmploymentType type = EmploymentType.FullTime, SalaryEmploymentStatus status = SalaryEmploymentStatus.CurrentEmployee,
        SalaryCurrency currency = SalaryCurrency.TRY, int? periodStart = 2024, int? periodEnd = null) =>
        new(occupation ?? Backend, years, type, status, amount, currency, hasBonus, bonus, periodStart, periodEnd);

    private static CompanySalaryRequest Former(int? periodStart, int? periodEnd) =>
        Request(status: SalaryEmploymentStatus.FormerEmployee, periodStart: periodStart, periodEnd: periodEnd);

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

    // ---- Period ------------------------------------------------------------------------------

    [Fact]
    public void Accepts_A_Former_Employees_Closed_Period()
    {
        Validator().Validate(Former(2010, 2012)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void The_Start_Year_Is_Required_Even_Though_The_Shape_Allows_Null()
    {
        Validator().Validate(Request(periodStart: null))
            .Errors.ShouldContain(e => e.PropertyName == "PeriodStartYear" && e.ErrorMessage == "VALIDATION_SALARY_PERIOD_START_REQUIRED");
    }

    [Theory]
    [InlineData(1989)]
    [InlineData(2027)]
    public void The_Start_Year_Stays_Between_1990_And_This_Year(int start)
    {
        Validator().Validate(Request(periodStart: start))
            .Errors.ShouldContain(e => e.PropertyName == "PeriodStartYear" && e.ErrorMessage == "VALIDATION_SALARY_PERIOD_YEAR_OUT_OF_RANGE");
    }

    [Fact]
    public void A_Current_Employee_Refuses_An_End_Year()
    {
        Validator().Validate(Request(periodEnd: 2025))
            .Errors.ShouldContain(e => e.PropertyName == "PeriodEndYear" && e.ErrorMessage == "VALIDATION_SALARY_PERIOD_END_UNEXPECTED");
    }

    [Fact]
    public void A_Former_Employee_Needs_An_End_Year()
    {
        Validator().Validate(Former(2010, null))
            .Errors.ShouldContain(e => e.PropertyName == "PeriodEndYear" && e.ErrorMessage == "VALIDATION_SALARY_PERIOD_END_REQUIRED");
    }

    [Fact]
    public void The_End_Year_Cannot_Precede_The_Start()
    {
        Validator().Validate(Former(2012, 2010))
            .Errors.ShouldContain(e => e.PropertyName == "PeriodEndYear" && e.ErrorMessage == "VALIDATION_SALARY_PERIOD_END_BEFORE_START");
    }

    [Fact]
    public void The_End_Year_Cannot_Be_In_The_Future()
    {
        Validator().Validate(Former(2012, 2027))
            .Errors.ShouldContain(e => e.PropertyName == "PeriodEndYear" && e.ErrorMessage == "VALIDATION_SALARY_PERIOD_YEAR_OUT_OF_RANGE");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void List_Query_Page_Is_Bounded(int page)
    {
        new CompanySalaryListQueryValidator().Validate(new CompanySalaryListQuery(page)).IsValid.ShouldBeFalse();
    }
}
