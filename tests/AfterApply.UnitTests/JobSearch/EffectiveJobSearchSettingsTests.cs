using AfterApply.Application.JobSearch;
using AfterApply.Application.JobSearch.Contracts;
using AfterApply.Domain.JobSearch;
using Shouldly;

namespace AfterApply.UnitTests.JobSearch;

/// <summary>
/// The country/language rule is the part of job search most likely to quietly return nothing,
/// so it is pinned in full: country always resolves (user, then global "tr"); language resolves
/// only when someone actually set one — there is no global language to fall back to, on purpose.
/// </summary>
public class EffectiveJobSearchSettingsTests
{
    private static readonly JobSearchGlobalDefaults Global = new("tr", 3, 5, 10);
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void No_User_Row_Means_Turkey_And_No_Language()
    {
        var effective = EffectiveJobSearchSettings.Resolve(null, Global);

        effective.Country.ShouldBe("tr");
        effective.Language.ShouldBeNull();
        effective.Location.ShouldBeNull();
        effective.DatePosted.ShouldBe(JobSearchDatePosted.All);
        effective.WorkFromHome.ShouldBeFalse();
        effective.PerUserDailyCredits.ShouldBe(10);
        effective.MaxPagesPerSearch.ShouldBe(3);
        effective.MaxJobIdsPerDetails.ShouldBe(5);
    }

    [Fact]
    public void A_Users_Country_Wins_Over_The_Global_One()
    {
        var row = JobSearchUserSettings.CreateEmpty(Guid.NewGuid(), Now);
        row.SetPreferences("DE", null, null, null, null, Now);

        EffectiveJobSearchSettings.Resolve(row, Global).Country.ShouldBe("de");
    }

    [Fact]
    public void A_Users_Language_Is_The_Only_Way_A_Language_Appears()
    {
        var row = JobSearchUserSettings.CreateEmpty(Guid.NewGuid(), Now);
        row.SetPreferences(null, "EN", null, null, null, Now);

        var effective = EffectiveJobSearchSettings.Resolve(row, Global);
        effective.Language.ShouldBe("en");
        effective.Country.ShouldBe("tr");
    }

    [Fact]
    public void Blank_Overrides_Fall_Back_Like_Absent_Ones()
    {
        var row = JobSearchUserSettings.CreateEmpty(Guid.NewGuid(), Now);
        row.SetPreferences("  ", "", " ", null, null, Now);

        var effective = EffectiveJobSearchSettings.Resolve(row, Global);
        effective.Country.ShouldBe("tr");
        effective.Language.ShouldBeNull();
        effective.Location.ShouldBeNull();
        row.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void A_Blank_Global_Country_Still_Resolves_To_Turkey()
    {
        EffectiveJobSearchSettings.Resolve(null, Global with { DefaultCountry = "" }).Country.ShouldBe("tr");
    }

    [Fact]
    public void Preferences_And_Limits_Merge_Field_By_Field()
    {
        var row = JobSearchUserSettings.CreateEmpty(Guid.NewGuid(), Now);
        row.SetPreferences(null, null, "İstanbul", nameof(JobSearchDatePosted.Week), true, Now);
        row.SetLimits(25, null, 8, Now);

        var effective = EffectiveJobSearchSettings.Resolve(row, Global);
        effective.Location.ShouldBe("İstanbul");
        effective.DatePosted.ShouldBe(JobSearchDatePosted.Week);
        effective.WorkFromHome.ShouldBeTrue();
        effective.PerUserDailyCredits.ShouldBe(25);
        effective.MaxPagesPerSearch.ShouldBe(3);
        effective.MaxJobIdsPerDetails.ShouldBe(8);
    }

    [Fact]
    public void An_Unknown_Stored_Date_Filter_Falls_Back_To_All()
    {
        var row = JobSearchUserSettings.CreateEmpty(Guid.NewGuid(), Now);
        row.SetPreferences(null, null, null, "Fortnight", null, Now);

        EffectiveJobSearchSettings.Resolve(row, Global).DatePosted.ShouldBe(JobSearchDatePosted.All);
    }
}
