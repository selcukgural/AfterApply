using AfterApply.Domain.Common;
using AfterApply.Domain.CompanySalaries;
using Shouldly;

namespace AfterApply.UnitTests.CompanySalaries;

public class CompanySalaryEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid Backend = Guid.NewGuid();
    private static readonly Guid Staff = Guid.NewGuid();

    private static SalaryContent Content(Guid? occupation = null, int years = 6,
        decimal amount = 95_000m, decimal? bonus = 120_000m, SalaryCurrency currency = SalaryCurrency.TRY,
        SalaryEmploymentStatus status = SalaryEmploymentStatus.CurrentEmployee, int periodStart = 2024, int? periodEnd = null) =>
        new(occupation ?? Backend, years, EmploymentType.FullTime, status, amount, currency, bonus, periodStart, periodEnd);

    private static SalaryContent Former(int periodStart, int? periodEnd) =>
        Content(status: SalaryEmploymentStatus.FormerEmployee, periodStart: periodStart, periodEnd: periodEnd);

    [Fact]
    public void Create_Keeps_The_Occupation_And_Stamps_The_Times()
    {
        var entry = CompanySalaryEntry.Create(Guid.NewGuid(), Guid.NewGuid(), Content(), Now);

        entry.OccupationId.ShouldBe(Backend);
        entry.PeriodStartYear.ShouldBe(2024);
        entry.PeriodEndYear.ShouldBeNull();
        entry.SubmittedAt.ShouldBe(Now);
        entry.CreatedAt.ShouldBe(Now);
        entry.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public void A_Former_Employee_Keeps_Both_Ends_Of_The_Period()
    {
        var entry = CompanySalaryEntry.Create(Guid.NewGuid(), Guid.NewGuid(), Former(2010, 2012), Now);

        entry.PeriodStartYear.ShouldBe(2010);
        entry.PeriodEndYear.ShouldBe(2012);
    }

    [Fact]
    public void Amounts_Are_Rounded_To_Two_Places()
    {
        var entry = CompanySalaryEntry.Create(Guid.NewGuid(), Guid.NewGuid(), Content(amount: 45_000.555m, bonus: 10_000.004m), Now);

        entry.MonthlyNetAmount.ShouldBe(45_000.56m);
        entry.AnnualBonusAmount.ShouldBe(10_000.00m);
    }

    [Fact]
    public void No_Bonus_Round_Trips_As_Null()
    {
        var entry = CompanySalaryEntry.Create(Guid.NewGuid(), Guid.NewGuid(), Content(bonus: null), Now);

        entry.AnnualBonusAmount.ShouldBeNull();
    }

    [Fact]
    public void Edit_Replaces_The_Figures_And_Resets_The_Month()
    {
        var entry = CompanySalaryEntry.Create(Guid.NewGuid(), Guid.NewGuid(), Content(), Now);
        var later = Now.AddMonths(2);

        entry.Edit(Content(occupation: Staff, years: 8, amount: 120_000m, bonus: null, currency: SalaryCurrency.EUR,
            status: SalaryEmploymentStatus.FormerEmployee, periodStart: 2021, periodEnd: 2026), later);

        entry.OccupationId.ShouldBe(Staff);
        entry.YearsOfExperience.ShouldBe(8);
        entry.MonthlyNetAmount.ShouldBe(120_000m);
        entry.Currency.ShouldBe(SalaryCurrency.EUR);
        entry.AnnualBonusAmount.ShouldBeNull();
        entry.PeriodStartYear.ShouldBe(2021);
        entry.PeriodEndYear.ShouldBe(2026);
        entry.SubmittedAt.ShouldBe(later);
        entry.UpdatedAt.ShouldBe(later);
        entry.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Rejects_A_Missing_Occupation()
    {
        Should.Throw<SalaryContentInvalidException>(() => Content(Guid.Empty).Validate(Now.Year));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    public void Rejects_Years_Off_The_Scale(int years)
    {
        Should.Throw<SalaryContentInvalidException>(() => Content(years: years).Validate(Now.Year));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_000_001)]
    public void Rejects_An_Amount_Off_The_Scale(decimal amount)
    {
        Should.Throw<SalaryContentInvalidException>(() => Content(amount: amount).Validate(Now.Year));
        Should.Throw<SalaryContentInvalidException>(() => Content(bonus: amount).Validate(Now.Year));
    }

    [Fact]
    public void Rejects_An_Undefined_Enum_Value()
    {
        Should.Throw<SalaryContentInvalidException>(() => Content(currency: (SalaryCurrency)42).Validate(Now.Year));
    }

    // ---- Period ------------------------------------------------------------------------------

    [Theory]
    [InlineData(1989)]
    [InlineData(2027)]
    [InlineData(0)]
    public void Rejects_A_Start_Year_Before_1990_Or_After_This_Year(int start)
    {
        Should.Throw<SalaryContentInvalidException>(() => Content(periodStart: start).Validate(Now.Year));
    }

    [Fact]
    public void A_Current_Employee_Has_No_End_Year()
    {
        Should.Throw<SalaryContentInvalidException>(() => Content(periodEnd: 2025).Validate(Now.Year));
    }

    [Fact]
    public void A_Former_Employee_Must_Say_When_It_Ended()
    {
        Should.Throw<SalaryContentInvalidException>(() => Former(2010, null).Validate(Now.Year));
    }

    [Theory]
    [InlineData(2012, 2010)]
    [InlineData(2012, 2027)]
    public void A_Former_Employees_End_Year_Stays_Between_The_Start_And_This_Year(int start, int end)
    {
        Should.Throw<SalaryContentInvalidException>(() => Former(start, end).Validate(Now.Year));
    }

    [Fact]
    public void A_Period_Of_One_Year_Is_Fine()
    {
        Should.NotThrow(() => Former(2026, 2026).Validate(Now.Year));
        Should.NotThrow(() => Content(periodStart: 2026).Validate(Now.Year));
    }
}
