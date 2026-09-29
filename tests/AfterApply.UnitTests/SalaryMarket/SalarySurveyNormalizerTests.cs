using AfterApply.Application.SalaryMarket;
using AfterApply.Application.SalaryMarket.Import;
using Shouldly;

namespace AfterApply.UnitTests.SalaryMarket;

public class SalarySurveyNormalizerTests
{
    // Real labels from the 2018–2026 files, including the ones a naive substring match gets wrong.
    [Theory]
    [InlineData("Full Stack Developer", "full-stack-developer")]
    [InlineData("Back-end Developer", "back-end-developer")]
    [InlineData("Back-end Developer | .Net", "back-end-developer")]
    [InlineData("Front-end Developer", "front-end-developer")]
    [InlineData("Mobile Application Developer (Full Stack)", "mobile-developer")]
    [InlineData("Mobile Developer | iOS", "mobile-developer")]
    [InlineData("Embedded Software Developer", "embedded-software-developer")]
    [InlineData("Site Reliability Engineer", "devops-engineer")]
    [InlineData("SRE", "devops-engineer")]
    [InlineData("Cloud Engineer", "devops-engineer")]
    [InlineData("QA / Manuel Test", "qa-engineer")]
    [InlineData("Test Automation Engineer", "qa-engineer")]
    [InlineData("AI Engineer", "ai-engineer")]
    [InlineData("Machine Learning Engineer", "data-scientist")]
    [InlineData("Data Analyst", "data-analyst")]
    [InlineData("Data Engineer", "data-engineer")]
    [InlineData("Game Developer", "game-developer")]
    [InlineData("Software Architect", "software-architect")]
    [InlineData("Software Development Team Lead", "team-lead")]
    [InlineData("Director of Software Development", "software-director")]
    [InlineData("CTO / Software Development Manager", "engineering-manager")]
    [InlineData("CTO", "cto")]
    [InlineData("Software Development Manager / Engineering Manager", "engineering-manager")]
    [InlineData("Product Owner", "product-manager")]
    [InlineData("Project & Product Manager / PO", "product-manager")]
    [InlineData("Project Manager", "project-manager")]
    [InlineData("Business Analyst", "business-analyst")]
    [InlineData("SAP / ABAP Developer & Consultant", "sap-erp-developer")]
    [InlineData("Database Administrator (DBA)", "database-administrator")]
    [InlineData("Cyber Security", "cyber-security")]
    [InlineData("Support Engineer", "system-engineer")]
    [InlineData("Network & System Engineer", "system-engineer")]
    public void Maps_Survey_Positions_To_Their_Occupation(string label, string group) =>
        SalarySurveyNormalizer.GroupOf(label).ShouldBe(group);

    [Theory]
    [InlineData("Diğer")]
    [InlineData("UI/UX Designer")]
    [InlineData("IT Manager")]
    [InlineData("Software Engineer")]
    [InlineData("")]
    [InlineData(null)]
    public void Leaves_Positions_That_Are_No_Published_Occupation_Unmapped(string? label) =>
        SalarySurveyNormalizer.GroupOf(label).ShouldBeNull();

    [Fact]
    public void Every_Rule_Points_At_A_Published_Occupation()
    {
        var labels = new[] { "Full Stack Developer", "Back-end Developer", "AI Engineer", "CTO", "Support Engineer" };
        foreach (var label in labels)
        {
            SalaryMarketGroups.BySlug(SalarySurveyNormalizer.GroupOf(label)!).ShouldNotBeNull();
        }
    }

    [Theory]
    [InlineData("Junior", SalaryMarketLevel.Junior)]
    [InlineData("Middle", SalaryMarketLevel.Middle)]
    [InlineData("Mid", SalaryMarketLevel.Middle)]
    [InlineData("Senior", SalaryMarketLevel.Senior)]
    [InlineData("Guru", SalaryMarketLevel.Senior)]
    [InlineData("MIDDLE", SalaryMarketLevel.Middle)]
    public void Folds_Levels_Onto_Three_Steps(string level, SalaryMarketLevel expected) =>
        SalarySurveyNormalizer.LevelOf(level).ShouldBe(expected);

    [Fact]
    public void An_Unknown_Level_Is_No_Level() => SalarySurveyNormalizer.LevelOf("Lead").ShouldBeNull();

    [Theory]
    [InlineData("0 - 1 Yıl", SalaryMarketExperience.ZeroToTwo)]
    [InlineData("1 - 3 Yıl", SalaryMarketExperience.ZeroToTwo)]
    [InlineData("0-2", SalaryMarketExperience.ZeroToTwo)]
    [InlineData("3 - 5 Yıl", SalaryMarketExperience.ThreeToFive)]
    [InlineData("5 - 7 Yıl", SalaryMarketExperience.SixToTen)]
    [InlineData("7 - 10 Yıl", SalaryMarketExperience.SixToTen)]
    [InlineData("6-10", SalaryMarketExperience.SixToTen)]
    [InlineData("10 - 12 Yıl", SalaryMarketExperience.TenPlus)]
    [InlineData("15 Yıl ve üzeri", SalaryMarketExperience.TenPlus)]
    [InlineData("10 yıldan daha fazla", SalaryMarketExperience.TenPlus)]
    [InlineData("10+", SalaryMarketExperience.TenPlus)]
    public void Folds_Experience_Onto_The_Four_Shared_Ranges(string experience, SalaryMarketExperience expected) =>
        SalarySurveyNormalizer.ExperienceOf(experience).ShouldBe(expected);

    [Fact]
    public void Experience_Without_A_Number_Is_Unknown() => SalarySurveyNormalizer.ExperienceOf("bilmiyorum").ShouldBeNull();

    private static SurveyResponse Answer(string? currency = "₺ - Türk Lirası", string? city = "İstanbul",
        string? salary = "100.000 - 104.999", string? position = "Back-end Developer") =>
        new(2026, "onceki-yazilimci", position, "Senior", "5 - 7 Yıl", city, currency, salary);

    [Fact]
    public void A_Turkish_Lira_Answer_In_Turkey_Is_Kept_With_Every_Scale_Filled()
    {
        var (response, exclusion) = SalarySurveyNormalizer.Normalize(Answer());

        exclusion.ShouldBeNull();
        response.ShouldNotBeNull();
        response.Group.ShouldBe("back-end-developer");
        response.Level.ShouldBe(SalaryMarketLevel.Senior);
        response.Experience.ShouldBe(SalaryMarketExperience.SixToTen);
        response.Salary.Value.ShouldBe(102500m);
    }

    [Fact]
    public void An_Early_Year_Without_A_Currency_Question_Counts_As_Lira() =>
        SalarySurveyNormalizer.Normalize(Answer(currency: null)).Exclusion.ShouldBeNull();

    [Theory]
    [InlineData("$ - Dolar")]
    [InlineData("€ - Euro")]
    [InlineData("")]
    public void Another_Currency_Is_Left_Out(string currency) =>
        SalarySurveyNormalizer.Normalize(Answer(currency: currency)).Exclusion.ShouldBe(SurveyExclusion.ForeignCurrency);

    [Theory]
    [InlineData("Yurt Dışı")]
    [InlineData("Yurtdışı")]
    [InlineData("* Almanya")]
    public void Working_Abroad_Is_Left_Out(string city) =>
        SalarySurveyNormalizer.Normalize(Answer(city: city)).Exclusion.ShouldBe(SurveyExclusion.Abroad);

    [Fact]
    public void No_Salary_Is_Left_Out() =>
        SalarySurveyNormalizer.Normalize(Answer(salary: null)).Exclusion.ShouldBe(SurveyExclusion.NoSalary);

    [Fact]
    public void An_Unknown_Position_Is_Left_Out() =>
        SalarySurveyNormalizer.Normalize(Answer(position: "Diğer")).Exclusion.ShouldBe(SurveyExclusion.UnknownPosition);
}
