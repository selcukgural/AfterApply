using AfterApply.Application.SiteTraffic;
using AfterApply.Domain.SiteTraffic;
using Shouldly;

namespace AfterApply.UnitTests.SiteTraffic;

/// <summary>
/// The normaliser is where the counter's privacy promise is actually enforced, so these tests are
/// less about parsing and more about what must never reach the database.
/// </summary>
public class SiteTrafficNormalizerTests
{
    [Fact]
    public void A_Public_Page_Is_Split_Into_Language_And_Path()
    {
        var result = SiteTrafficNormalizer.Normalize("page_view", "/tr/guide/how-many-applications", null);

        result.ShouldNotBeNull();
        result.Event.ShouldBe(SiteTrafficEvent.PageView);
        result.Locale.ShouldBe("tr");
        result.Path.ShouldBe("/guide/how-many-applications");
        result.ReferrerHost.ShouldBe(string.Empty);
    }

    [Fact]
    public void The_Landing_Page_Normalises_To_A_Single_Slash()
    {
        SiteTrafficNormalizer.Normalize("page_view", "/en", null)!.Path.ShouldBe("/");
        SiteTrafficNormalizer.Normalize("page_view", "/en/", null)!.Path.ShouldBe("/");
    }

    // The reason the query string is cut before anything else: these are the two paths where it
    // carries a secret. Neither the token nor the OAuth code may reach a stored row — and the
    // callback path is not on the allowlist at all, so it is dropped outright.
    [Theory]
    [InlineData("/tr/reset-password?token=abc123secret", "/reset-password")]
    [InlineData("/tr/login?next=%2Ftr%2Fdashboard", "/login")]
    [InlineData("/tr/guide?utm_source=twitter#top", "/guide")]
    public void The_Query_String_And_Fragment_Are_Discarded(string path, string expected)
    {
        var result = SiteTrafficNormalizer.Normalize("page_view", path, null);

        result.ShouldNotBeNull();
        result.Path.ShouldBe(expected);
        result.Path.ShouldNotContain("?");
        result.Path.ShouldNotContain("secret");
    }

    /// <summary>
    /// The CV scan's own funnel: the page, then the event that says a scan actually finished. The
    /// second is what separates "people arrive" from "people get an answer", which is the whole
    /// measurement the surface is judged on (DEVELOPMENT_PLAN.md, V6).
    /// </summary>
    [Fact]
    public void The_Cv_Scan_Page_And_Its_Completion_Are_Countable()
    {
        var view = SiteTrafficNormalizer.Normalize("page_view", "/tr/cv-tarama", null);
        view.ShouldNotBeNull();
        view.Path.ShouldBe("/cv-tarama");

        var completed = SiteTrafficNormalizer.Normalize("cv_scan_completed", "/en/cv-tarama", null);
        completed.ShouldNotBeNull();
        completed.Event.ShouldBe(SiteTrafficEvent.CvScanCompleted);
        completed.Locale.ShouldBe("en");
    }

    /// <summary>
    /// The English slug (growth audit finding 14) and the shared-score pages. A score page is the
    /// address a share lands on, so arrivals there are the number that says whether sharing works;
    /// the card itself is dropped, so the row says "through a shared score", never which one.
    /// </summary>
    [Theory]
    [InlineData("/en/cv-scan", "/cv-scan")]
    [InlineData("/tr/hakkimizda", "/hakkimizda")]
    [InlineData("/en/about", "/about")]
    [InlineData("/tr/teklif-karsilastirma", "/teklif-karsilastirma")]
    [InlineData("/en/offer-comparison", "/offer-comparison")]
    [InlineData("/tr/cv-tarama/puan/88", "/cv-tarama/puan")]
    [InlineData("/tr/cv-tarama/puan/88-28-22-20-18", "/cv-tarama/puan")]
    [InlineData("/en/cv-scan/score/61-20-15-14-12", "/cv-scan/score")]
    public void The_English_Slug_And_The_Shared_Score_Pages_Are_Countable_Without_The_Card(string path, string expected)
    {
        var result = SiteTrafficNormalizer.Normalize("page_view", path, null);

        result.ShouldNotBeNull();
        result.Path.ShouldBe(expected);
    }

    [Theory]
    [InlineData("/tr/cv-tarama/puan")]
    [InlineData("/tr/cv-tarama/puan/abc")]
    [InlineData("/tr/cv-tarama/skor/88")]
    [InlineData("/tr/benchmark/puan/88")]
    [InlineData("/tr/cv-tarama/puan/88/extra")]
    public void A_Score_Page_With_The_Wrong_Shape_Is_Dropped(string path)
    {
        SiteTrafficNormalizer.Normalize("page_view", path, null).ShouldBeNull();
    }

    [Fact]
    public void The_Oauth_Callback_Is_Not_Countable_At_All()
    {
        SiteTrafficNormalizer.Normalize("page_view", "/tr/auth/google/callback?code=4/0Ab", null).ShouldBeNull();
        SiteTrafficNormalizer.Normalize("page_view", "/tr/auth/linkedin/callback", null).ShouldBeNull();
    }

    // The allowlist's real job. A signed-in path carries record ids; if one could be recorded, the
    // table would hold "someone looked at application 0192…" — exactly what it must never hold.
    [Theory]
    [InlineData("/tr/dashboard")]
    [InlineData("/tr/applications")]
    [InlineData("/tr/applications/0192e5c1-6f3a-7c2b-9a11-4f0d2e8b5a77")]
    [InlineData("/tr/settings")]
    [InlineData("/tr/admin/metrics")]
    [InlineData("/tr/cv")]
    public void Signed_In_Pages_Are_Never_Recorded(string path)
    {
        SiteTrafficNormalizer.Normalize("page_view", path, null).ShouldBeNull();
    }

    [Fact]
    public void A_Path_Without_A_Language_Prefix_Is_Not_A_Page_View()
    {
        // localePrefix is "always", so every real URL carries /tr or /en. Anything else was not a
        // page on this site.
        SiteTrafficNormalizer.Normalize("page_view", "/guide", null).ShouldBeNull();
        SiteTrafficNormalizer.Normalize("page_view", "/", null).ShouldBeNull();
        SiteTrafficNormalizer.Normalize("page_view", "/fr/guide", null).ShouldBeNull();
    }

    [Fact]
    public void A_Relative_Or_Absolute_Url_Is_Rejected()
    {
        SiteTrafficNormalizer.Normalize("page_view", "https://ekariyerim.com/tr/guide", null).ShouldBeNull();
        SiteTrafficNormalizer.Normalize("page_view", "tr/guide", null).ShouldBeNull();
    }

    [Fact]
    public void An_Overlong_Path_Is_Rejected_Rather_Than_Truncated()
    {
        var path = "/tr/guide/" + new string('a', SiteTrafficNormalizer.MaxPathLength);

        SiteTrafficNormalizer.Normalize("page_view", path, null).ShouldBeNull();
    }

    [Fact]
    public void A_Slug_Outside_The_Allowed_Shape_Is_Rejected()
    {
        // Bounded charset and length are what keep the table's row count finite when a caller
        // invents slugs.
        SiteTrafficNormalizer.Normalize("page_view", "/tr/guide/" + new string('a', 65), null).ShouldBeNull();
        SiteTrafficNormalizer.Normalize("page_view", "/tr/guide/-leading-dash", null).ShouldBeNull();
        SiteTrafficNormalizer.Normalize("page_view", "/tr/guide/a b", null).ShouldBeNull();
        SiteTrafficNormalizer.Normalize("page_view", "/tr/guide/slug/deeper", null).ShouldBeNull();
        SiteTrafficNormalizer.Normalize("page_view", "/tr/settings/slug", null).ShouldBeNull();
    }

    // This product's default culture is tr-TR, where "I".ToLower() is a dotless "ı". A path that
    // arrived with an uppercase letter must still match the allowlist.
    [Fact]
    public void Casing_Is_Folded_With_The_Invariant_Culture()
    {
        var result = SiteTrafficNormalizer.Normalize("page_view", "/TR/HELP/IMPORT", null);

        result.ShouldNotBeNull();
        result.Locale.ShouldBe("tr");
        result.Path.ShouldBe("/help/import");
    }

    [Fact]
    public void An_Unknown_Event_Name_Is_Dropped()
    {
        SiteTrafficNormalizer.Normalize("scroll_depth", "/tr", null).ShouldBeNull();
        SiteTrafficNormalizer.Normalize("", "/tr", null).ShouldBeNull();
        SiteTrafficNormalizer.Normalize(null, "/tr", null).ShouldBeNull();
    }

    [Fact]
    public void Every_Declared_Event_Has_A_Wire_Name()
    {
        // A member added to the enum but not to the normaliser's map would be unreportable — dead
        // storage that nothing could ever write.
        string[] wireNames =
            ["page_view", "cta_get_started", "cv_scan_completed", "register_started", "register_completed", "share_clicked"];

        var reachable = wireNames
            .Select(name => SiteTrafficNormalizer.Normalize(name, "/tr", null)!.Event)
            .ToHashSet();

        reachable.ShouldBe(Enum.GetValues<SiteTrafficEvent>().ToHashSet(), ignoreOrder: true);
    }

    [Theory]
    [InlineData("https://www.google.com/search?q=is+basvuru+takip", "google.com")]
    [InlineData("https://google.com/search?q=secret+search+term", "google.com")]
    [InlineData("https://eksisozluk.com/is-basvurusu--123456", "eksisozluk.com")]
    [InlineData("http://localhost:3000/tr", "localhost")]
    public void A_Referrer_Is_Reduced_To_Its_Host(string referrer, string expected)
    {
        var result = SiteTrafficNormalizer.Normalize("page_view", "/tr", referrer);

        result.ShouldNotBeNull();
        result.ReferrerHost.ShouldBe(expected);
        // The point of the whole rule: whatever someone searched for never survives the trip.
        result.ReferrerHost.ShouldNotContain("q=");
        result.ReferrerHost.ShouldNotContain("secret");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("/tr/dashboard")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    public void An_Unusable_Referrer_Becomes_Empty_Rather_Than_Rejecting_The_Report(string? referrer)
    {
        // A missing or malformed referrer is the normal case for a typed or bookmarked visit — it
        // must not cost the page view itself.
        var result = SiteTrafficNormalizer.Normalize("page_view", "/tr", referrer);

        result.ShouldNotBeNull();
        result.ReferrerHost.ShouldBe(string.Empty);
    }

    /// <summary>The public company pages: the directory itself, and one page per company slug —
    /// which slug brings anyone in is the number the feature is measured by, so it is kept, the
    /// way guide articles are.</summary>
    [Theory]
    [InlineData("/tr/companies", "/companies")]
    [InlineData("/en/companies/turk-telekom", "/companies/turk-telekom")]
    [InlineData("/tr/companies/scoring", "/companies/scoring")]
    public void Company_Pages_Are_Counted(string path, string expected)
    {
        var result = SiteTrafficNormalizer.Normalize("page_view", path, null);

        result.ShouldNotBeNull();
        result.Path.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36", false)]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1", false)]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:130.0) Gecko/20100101 Firefox/130.0", false)]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)", true)]
    [InlineData("Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)", true)]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) HeadlessChrome/129.0.0.0 Safari/537.36", true)]
    [InlineData("Mozilla/5.0 (Linux; Android 11; moto g power) Chrome/129 Mobile Safari/537.36 Chrome-Lighthouse", true)]
    [InlineData("curl/8.4.0", true)]
    [InlineData("python-requests/2.32", true)]
    [InlineData("Go-http-client/1.1", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void A_User_Agent_Is_Classified_As_Human_Or_Not(string? userAgent, bool nonHuman)
    {
        // Real browsers pass; crawlers, audit tools, headless browsers, HTTP libraries and an absent
        // header (every real browser sends one) do not. The header is compared and forgotten.
        SiteTrafficNormalizer.IsNonHumanUserAgent(userAgent).ShouldBe(nonHuman);
    }
}
