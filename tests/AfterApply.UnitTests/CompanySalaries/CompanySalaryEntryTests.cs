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
        decimal amount = 95_000m, decimal? bonus = 120_000m, SalaryCurrency currency = SalaryCurrency.TRY) =>
        new(occupation ?? Backend, years, EmploymentType.FullTime, SalaryEmploymentStatus.CurrentEmployee, amount, currency, bonus);

    [Fact]
    public void Create_Keeps_The_Occupation_And_Stamps_The_Times()
    {
        var entry = CompanySalaryEntry.Create(Guid.NewGuid(), Guid.NewGuid(), Content(), Now);

        entry.OccupationId.ShouldBe(Backend);
        entry.SubmittedAt.ShouldBe(Now);
        entry.CreatedAt.ShouldBe(Now);
        entry.UpdatedAt.ShouldBe(Now);
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

        entry.Edit(Content(occupation: Staff, years: 8, amount: 120_000m, bonus: null, currency: SalaryCurrency.EUR), later);

        entry.OccupationId.ShouldBe(Staff);
        entry.YearsOfExperience.ShouldBe(8);
        entry.MonthlyNetAmount.ShouldBe(120_000m);
        entry.Currency.ShouldBe(SalaryCurrency.EUR);
        entry.AnnualBonusAmount.ShouldBeNull();
        entry.SubmittedAt.ShouldBe(later);
        entry.UpdatedAt.ShouldBe(later);
        entry.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Rejects_A_Missing_Occupation()
    {
        Should.Throw<SalaryContentInvalidException>(() => Content(Guid.Empty).Validate());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    public void Rejects_Years_Off_The_Scale(int years)
    {
        Should.Throw<SalaryContentInvalidException>(() => Content(years: years).Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_000_001)]
    public void Rejects_An_Amount_Off_The_Scale(decimal amount)
    {
        Should.Throw<SalaryContentInvalidException>(() => Content(amount: amount).Validate());
        Should.Throw<SalaryContentInvalidException>(() => Content(bonus: amount).Validate());
    }

    [Fact]
    public void Rejects_An_Undefined_Enum_Value()
    {
        Should.Throw<SalaryContentInvalidException>(() => Content(currency: (SalaryCurrency)42).Validate());
    }
}
