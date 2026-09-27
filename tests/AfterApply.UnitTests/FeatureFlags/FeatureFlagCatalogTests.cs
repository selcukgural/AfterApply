using AfterApply.Application.FeatureFlags;
using AfterApply.Application.FeatureFlags.Contracts;
using AfterApply.Application.FeatureFlags.Validators;
using AfterApply.Application.Localization;
using AfterApply.Domain.FeatureFlags;
using AfterApply.Infrastructure.Blog;
using AfterApply.Infrastructure.Board;
using AfterApply.Infrastructure.CvScan;
using AfterApply.Infrastructure.Documents;
using AfterApply.Infrastructure.FeatureFlags;
using AfterApply.Infrastructure.Feedback;
using AfterApply.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.FeatureFlags;

public class FeatureFlagCatalogTests
{
    /// <summary>The catalog over options bound from <paramref name="settings"/>, the way the app binds them.</summary>
    private static FeatureFlagCatalog Catalog(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build();
        var services = new ServiceCollection();
        services.Configure<BoardOptions>(configuration.GetSection(BoardOptions.SectionName));
        services.Configure<BlogOptions>(configuration.GetSection(BlogOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<CvScanOptions>(configuration.GetSection(CvScanOptions.SectionName));
        services.Configure<FeedbackGitHubOptions>(configuration.GetSection(FeedbackGitHubOptions.SectionName));
        services.Configure<PayTrOptions>(configuration.GetSection(PayTrOptions.SectionName));
        // Every other section on its code defaults.
        services.AddOptions();
        return new FeatureFlagCatalog(services.BuildServiceProvider());
    }

    [Fact]
    public void Every_Flag_Has_A_Definition()
    {
        FeatureFlagCatalog.Defined.ShouldBe(Enum.GetValues<FeatureFlag>(), ignoreOrder: true);
    }

    [Fact]
    public void The_Default_Is_The_Options_Section_Enabled_Value()
    {
        var catalog = Catalog(new() { ["Board:Enabled"] = "true", ["CvScan:LlmEnabled"] = "true", ["Blog:Enabled"] = "false" });

        catalog.DefaultOf(FeatureFlag.Board).ShouldBeTrue();
        catalog.DefaultOf(FeatureFlag.CvScanNotes).ShouldBeTrue();
        catalog.DefaultOf(FeatureFlag.Blog).ShouldBeFalse();
        // Untouched sections keep their code defaults.
        catalog.DefaultOf(FeatureFlag.CompanyReviews).ShouldBeTrue();
        catalog.DefaultOf(FeatureFlag.CompanyIntelligence).ShouldBeFalse();
        catalog.DefaultOf(FeatureFlag.Payments).ShouldBeFalse();
    }

    [Fact]
    public void A_Flag_Missing_Its_Configuration_Names_What_Is_Missing()
    {
        var catalog = Catalog(new() { ["Storage:Provider"] = "GoogleCloudStorage" });

        catalog.MissingPrerequisiteOf(FeatureFlag.Blog).ShouldBe(FeatureFlagPrerequisites.BlogMediaBucket);
        catalog.MissingPrerequisiteOf(FeatureFlag.CvScanNotes).ShouldBe(FeatureFlagPrerequisites.CvScanNotesProject);
        catalog.MissingPrerequisiteOf(FeatureFlag.FeedbackGitHub).ShouldBe(FeatureFlagPrerequisites.FeedbackGitHubTarget);
        catalog.MissingPrerequisiteOf(FeatureFlag.Payments).ShouldBe(FeatureFlagPrerequisites.PayTrConfiguration);
        catalog.MissingPrerequisiteOf(FeatureFlag.Board).ShouldBeNull();
    }

    [Fact]
    public void Configured_Flags_Have_Nothing_Missing()
    {
        var catalog = Catalog(new()
        {
            ["Storage:Provider"] = "GoogleCloudStorage",
            ["Storage:BlogMediaBucketName"] = "blog-media",
            ["CvScan:Review:ProjectId"] = "project",
            ["Feedback:GitHub:Repository"] = "owner/repo",
            ["Feedback:GitHub:Token"] = "token",
            ["PayTr:MerchantId"] = "1",
            ["PayTr:MerchantKey"] = "k",
            ["PayTr:MerchantSalt"] = "s",
            ["PayTr:Plans:Monthly:AmountMinor"] = "29900",
            ["PayTr:Plans:Yearly:AmountMinor"] = "299000"
        });

        FeatureFlagCatalog.Defined.ShouldAllBe(flag => catalog.MissingPrerequisiteOf(flag) == null);
    }

    [Fact]
    public void The_Blog_Needs_No_Bucket_On_Local_Storage()
    {
        Catalog(new() { ["Storage:Provider"] = "FileSystem" }).MissingPrerequisiteOf(FeatureFlag.Blog).ShouldBeNull();
    }

    [Fact]
    public void What_Moves_With_A_Flag_Is_Named()
    {
        var catalog = Catalog();

        catalog.CouplingsOf(FeatureFlag.Payments).ShouldBe([FeatureFlagCoupling.Money]);
        catalog.CouplingsOf(FeatureFlag.FeedbackGitHub).ShouldBe([FeatureFlagCoupling.PrivacyText]);
        catalog.CouplingsOf(FeatureFlag.CvScanNotes).ShouldBe([FeatureFlagCoupling.PrivacyText, FeatureFlagCoupling.Money],
            ignoreOrder: true);
        catalog.CouplingsOf(FeatureFlag.Board).ShouldBeEmpty();
    }
}

public class FeatureFlagNamesTests
{
    [Theory]
    [InlineData("Board", FeatureFlag.Board)]
    [InlineData("board", FeatureFlag.Board)]
    [InlineData("CVSCANNOTES", FeatureFlag.CvScanNotes)]
    public void Names_Are_Read_Case_Insensitively(string name, FeatureFlag expected)
    {
        FeatureFlagNames.TryParse(name, out var flag).ShouldBeTrue();
        flag.ShouldBe(expected);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("99")]
    [InlineData("Board,Blog")]
    [InlineData(" Board")]
    [InlineData("")]
    [InlineData(null)]
    public void Numbers_Lists_And_Unknown_Names_Are_Not_Flags(string? name)
    {
        FeatureFlagNames.TryParse(name, out _).ShouldBeFalse();
    }
}

public class FeatureFlagChangeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_Origin_Keeps_A_Trimmed_Address_And_Never_A_Blank_One()
    {
        var change = FeatureFlagChange.Record("Board", true, false, true, "  Launch  ", Guid.NewGuid(), Now);
        change.Reason.ShouldBe("Launch");

        FeatureFlagChangeOrigin.For(change, Guid.NewGuid(), " 203.0.113.9 ").IpAddress.ShouldBe("203.0.113.9");
        FeatureFlagChangeOrigin.For(change, Guid.NewGuid(), "   ").IpAddress.ShouldBeNull();
        FeatureFlagChangeOrigin.For(change, Guid.NewGuid(), new string('a', 80)).IpAddress!.Length
            .ShouldBe(FeatureFlagChangeOrigin.MaxIpAddressLength);
    }
}

public class FeatureFlagValidatorTests
{
    private readonly PrepareFeatureFlagChangeRequestValidator _prepare = new(new KeyEchoLocalizer());
    private readonly ConfirmFeatureFlagChangeRequestValidator _confirm = new(new KeyEchoLocalizer());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData(" why ")]
    public void A_Reason_Of_At_Least_Five_Characters_Is_Required(string? reason)
    {
        _prepare.Validate(new PrepareFeatureFlagChangeRequest(true, reason)).Errors
            .ShouldContain(e => e.ErrorCode == "FEATURE_FLAG_REASON_REQUIRED");
    }

    [Fact]
    public void The_Reason_Is_Capped()
    {
        _prepare.Validate(new PrepareFeatureFlagChangeRequest(null, new string('x', FeatureFlagChange.MaxReasonLength + 1)))
            .Errors.ShouldContain(e => e.ErrorCode == "FEATURE_FLAG_REASON_TOO_LONG");
        _prepare.Validate(new PrepareFeatureFlagChangeRequest(false, "Rolling back a bad launch")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Both_Confirmation_Fields_Are_Required_And_Bounded()
    {
        _confirm.Validate(new ConfirmFeatureFlagChangeRequest(null, null)).Errors.Count.ShouldBe(2);
        _confirm.Validate(new ConfirmFeatureFlagChangeRequest(new string('t', FeatureFlagLimits.MaxTokenLength + 1), "Board"))
            .IsValid.ShouldBeFalse();
        _confirm.Validate(new ConfirmFeatureFlagChangeRequest("token", "Board")).IsValid.ShouldBeTrue();
    }

    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}

/// <summary>The admin panel shows these texts next to each switch; a flag without them would be
/// a switch nobody can safely read.</summary>
public class FeatureFlagTextTests
{
    private static readonly System.Resources.ResourceManager Resources =
        new("AfterApply.Application.Localization.SharedStrings", typeof(SharedStrings).Assembly);

    private static System.Resources.ResourceSet Set(string culture) =>
        Resources.GetResourceSet(System.Globalization.CultureInfo.GetCultureInfo(culture), createIfNotExists: true,
            tryParents: false)!;

    [Theory]
    [InlineData("tr")]
    [InlineData("")]
    public void Every_Flag_Says_What_It_Is_And_What_Off_Means_In_Both_Languages(string culture)
    {
        var set = Set(culture);
        foreach (var flag in Enum.GetValues<FeatureFlag>())
        {
            var prefix = FeatureFlagTexts.KeyPrefix(flag);
            foreach (var suffix in new[] { "TITLE", "DESCRIPTION", "WHEN_OFF" })
            {
                set.GetString($"{prefix}_{suffix}").ShouldNotBeNullOrWhiteSpace($"{prefix}_{suffix} ({culture})");
            }
        }
    }

    [Fact]
    public void Notes_Exist_In_Both_Languages_Or_In_Neither()
    {
        var turkish = Set("tr");
        var english = Set("");
        foreach (var flag in Enum.GetValues<FeatureFlag>())
        {
            var key = $"{FeatureFlagTexts.KeyPrefix(flag)}_NOTES";
            (turkish.GetString(key) is null).ShouldBe(english.GetString(key) is null, key);
        }
    }

    [Fact]
    public void Money_And_Privacy_Flags_Explain_Why_In_Their_Notes()
    {
        var catalog = new FeatureFlagCatalog(new ServiceCollection().AddOptions().BuildServiceProvider());
        var english = Set("");
        foreach (var flag in Enum.GetValues<FeatureFlag>().Where(f => catalog.CouplingsOf(f).Count > 0))
        {
            english.GetString($"{FeatureFlagTexts.KeyPrefix(flag)}_NOTES").ShouldNotBeNullOrWhiteSpace(flag.ToString());
        }
    }

    [Theory]
    [InlineData(FeatureFlag.Board, "FEATURE_FLAG_BOARD")]
    [InlineData(FeatureFlag.CvScanNotes, "FEATURE_FLAG_CV_SCAN_NOTES")]
    [InlineData(FeatureFlag.FeedbackGitHub, "FEATURE_FLAG_FEEDBACK_GIT_HUB")]
    public void Keys_Are_The_Member_Name_In_Upper_Snake_Case(FeatureFlag flag, string expected)
    {
        FeatureFlagTexts.KeyPrefix(flag).ShouldBe(expected);
    }
}

public class FeatureFlagPollScheduleTests
{
    private static readonly FeatureFlagOptions Options = new();

    [Fact]
    public void Polls_Often_Until_The_Announcements_Can_Be_Heard()
    {
        var schedule = new FeatureFlagPollSchedule(Options);

        schedule.IsHearingAnnouncements.ShouldBeFalse();
        schedule.NextPoll.ShouldBe(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Polls_Rarely_While_Subscribed_And_Often_Again_Once_The_Connection_Is_Lost()
    {
        var schedule = new FeatureFlagPollSchedule(Options);

        schedule.Subscribed();
        schedule.NextPoll.ShouldBe(TimeSpan.FromMinutes(5));

        schedule.SubscriptionLost();
        schedule.NextPoll.ShouldBe(TimeSpan.FromSeconds(15));

        schedule.Subscribed();
        schedule.NextPoll.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void A_Zero_Or_Negative_Setting_Still_Waits_A_Second()
    {
        var schedule = new FeatureFlagPollSchedule(new FeatureFlagOptions { PollSeconds = 0, DegradedPollSeconds = -5 });

        schedule.NextPoll.ShouldBe(TimeSpan.FromSeconds(1));
        schedule.Subscribed();
        schedule.NextPoll.ShouldBe(TimeSpan.FromSeconds(1));
    }
}
