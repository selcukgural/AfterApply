using System.Net;
using AfterApply.Application.JobSources;
using AfterApply.Domain.Common;
using AfterApply.Domain.JobSources;
using AfterApply.Infrastructure.JobSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

public class KariyerNetJobSearchUrlBuilderTests
{
    [Theory]
    [InlineData("İstanbul", "istanbul")]
    [InlineData("İstanbul (Avrupa)", "istanbul-avrupa")]
    [InlineData("  Şanlıurfa ", "sanliurfa")]
    [InlineData("Muğla / Bodrum", "mugla-bodrum")]
    [InlineData("Çorum--", "corum")]
    [InlineData("i\u0307stanbul", "istanbul")]
    public void Slugs_The_City_The_Way_The_Site_Does(string given, string expected) =>
        KariyerNetJobSearchUrlBuilder.Slugify(given).ShouldBe(expected);

    [Fact]
    public void Builds_The_Listing_And_Its_Pages_And_Escapes_The_Keywords()
    {
        KariyerNetJobSearchUrlBuilder.Search(".net developer", "İstanbul", 1).AbsoluteUri
            .ShouldBe("https://www.kariyer.net/is-ilanlari/istanbul?kw=.net%20developer");
        KariyerNetJobSearchUrlBuilder.Search("c#/.net & \"senior\"", "Ankara", 3).AbsoluteUri
            .ShouldBe("https://www.kariyer.net/is-ilanlari/ankara-3?kw=c%23%2F.net%20%26%20%22senior%22&cp=3");
        KariyerNetJobSearchUrlBuilder.Posting("4555062").AbsoluteUri.ShouldBe("https://www.kariyer.net/is-ilani/4555062");
    }

    [Fact]
    public void Refuses_What_Cannot_Be_A_Path()
    {
        Should.Throw<ArgumentException>(() => KariyerNetJobSearchUrlBuilder.Search("x", "…", 1));
        Should.Throw<ArgumentException>(() => KariyerNetJobSearchUrlBuilder.Posting("../etc"));
        Should.Throw<ArgumentException>(() => KariyerNetJobSearchUrlBuilder.Posting(""));
    }

    [Fact]
    public void Knows_Whether_A_Redirect_Still_Answers_The_Search()
    {
        // The site's own 301: same path plus its city ids.
        KariyerNetJobSearchUrlBuilder.IsSearchFor(new Uri("https://www.kariyer.net/is-ilanlari/istanbul?ct=34,82&kw=x"), "İstanbul").ShouldBeTrue();
        KariyerNetJobSearchUrlBuilder.IsSearchFor(new Uri("https://www.kariyer.net/is-ilanlari/istanbul-2?ct=34,82&kw=x&cp=2"), "İstanbul").ShouldBeTrue();
        // An unknown city lands on the nationwide listing — not the answer.
        KariyerNetJobSearchUrlBuilder.IsSearchFor(new Uri("https://www.kariyer.net/is-ilanlari?kw=x"), "Atlantis").ShouldBeFalse();
        // A different city, or a city whose slug merely starts the same way.
        KariyerNetJobSearchUrlBuilder.IsSearchFor(new Uri("https://www.kariyer.net/is-ilanlari/ankara?kw=x"), "İstanbul").ShouldBeFalse();
        KariyerNetJobSearchUrlBuilder.IsSearchFor(new Uri("https://www.kariyer.net/is-ilanlari/izmirli?kw=x"), "İzmir").ShouldBeFalse();
        KariyerNetJobSearchUrlBuilder.IsSearchFor(new Uri("https://evil.example/is-ilanlari/istanbul"), "İstanbul").ShouldBeFalse();
    }
}

public class KariyerNetJobSearchCardParserTests
{
    private static readonly DateOnly Today = new(2026, 9, 14);

    [Fact]
    public void Reads_The_Cards_And_The_Pager()
    {
        var page = KariyerNetJobSearchCardParser.Parse(KariyerNetJobSourceFixtures.ThreeCardsWithNextPage, Today, currentPage: 1);

        page.HasMore.ShouldBeTrue();
        page.Cards.Count.ShouldBe(3);

        var first = page.Cards[0];
        first.ExternalId.ShouldBe("4460000001");
        first.Title.ShouldBe("Software Developer (.Net)");
        first.CompanyName.ShouldBe("Acme Yazılım A.Ş.");
        first.Location.ShouldBe("İstanbul");
        first.WorkModel.ShouldBe("İş Yerinde");
        first.PostedAt.ShouldBe(Today.AddDays(-3));
        first.Url.ShouldBe("https://www.kariyer.net/is-ilani/acme-yazilim-a-s-software-developer-net-4460000001");
        first.CompanyProfileUrl.ShouldBeNull();

        // Entities decoded, "12 saat" is today, "Dün" is yesterday.
        page.Cards[1].Title.ShouldBe("Backend Developer & Team Lead");
        page.Cards[1].WorkModel.ShouldBe("Uzaktan");
        page.Cards[1].PostedAt.ShouldBe(Today);
        page.Cards[2].PostedAt.ShouldBe(Today.AddDays(-1));
        page.Cards[2].Location.ShouldBe("İstanbul(Asya) +2 il daha");
    }

    [Fact]
    public void The_Last_Page_Has_No_More()
    {
        var page = KariyerNetJobSearchCardParser.Parse(KariyerNetJobSourceFixtures.LastPage, Today, currentPage: 2);

        page.Cards.Count.ShouldBe(1);
        page.HasMore.ShouldBeFalse();
        page.Cards[0].PostedAt.ShouldBe(Today.AddDays(-20));
    }

    [Fact]
    public void An_Empty_Or_Foreign_Page_Yields_Nothing()
    {
        KariyerNetJobSearchCardParser.Parse("<html><body>Aradığınız kriterlere uygun ilan bulunamadı</body></html>", Today, 1).Cards.ShouldBeEmpty();
        KariyerNetJobSearchCardParser.Parse(LinkedInJobSourceFixtures.ThreeCards, Today, 1).Cards.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("3 gün", -3)]
    [InlineData("update 21 saat", 0)]
    [InlineData("Bugün", 0)]
    [InlineData("Dün", -1)]
    [InlineData("45 dakika", 0)]
    public void Relative_Dates_Count_Back_From_Today(string text, int days) =>
        KariyerNetJobSearchCardParser.ParseRelativeDate(text, Today).ShouldBe(Today.AddDays(days));

    [Fact]
    public void An_Unreadable_Date_Is_Unknown_Not_Guessed()
    {
        KariyerNetJobSearchCardParser.ParseRelativeDate("", Today).ShouldBeNull();
        KariyerNetJobSearchCardParser.ParseRelativeDate("yakında", Today).ShouldBeNull();
        KariyerNetJobSearchCardParser.ParseRelativeDate("9999 gün", Today).ShouldBeNull();
    }
}

public class KariyerNetJobPostingParserTests
{
    [Fact]
    public void Flattens_The_Description_And_Reads_Experience_Type_And_Date()
    {
        var html = KariyerNetJobSourceFixtures.Posting(KariyerNetJobSourceFixtures.RichDescription);

        var detail = KariyerNetJobPostingParser.Parse(html);

        detail.Description.ShouldBe(
            "Kıdemli Yazılım Mühendisi (.NET)\n\nEkibimizde değerlendirmek üzere 7+ yıl deneyimli arıyoruz.\n\n" +
            "Aradığımız Nitelikler:\nC# ve .NET / .NET Core konusunda güçlü deneyim,\nREST API & web servisleri,\nİstanbul’da ikamet eden.");
        detail.Seniority.ShouldBe("En az 7 yıl tecrübeli");
        detail.EmploymentType.ShouldBe("Tam zamanlı");
        detail.JobFunction.ShouldBeNull();
        KariyerNetJobPostingParser.ParsePublishDate(html).ShouldBe(new DateOnly(2026, 9, 11));
    }

    [Fact]
    public void A_Page_Without_The_Block_Has_No_Description()
    {
        var detail = KariyerNetJobPostingParser.Parse("<html><body><p>Bu ilan yayından kaldırılmıştır.</p></body></html>");

        detail.Description.ShouldBeNull();
        detail.Seniority.ShouldBeNull();
        KariyerNetJobPostingParser.ParsePublishDate("<html></html>").ShouldBeNull();
    }

    [Fact]
    public void Nothing_Of_The_Markup_Survives()
    {
        var detail = KariyerNetJobPostingParser.Parse(KariyerNetJobSourceFixtures.Posting("<p>Hi <script>alert(1)</script><a href=\"x\">link</a></p>"));

        detail.Description.ShouldBe("Hi alert(1)link");
    }
}

public class KariyerNetJobSourceClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    private static readonly JobSourceQuery Query =
        JobSourceQuery.Create(Source.KariyerNet, ".net developer", "istanbul", JobSourceTimeWindow.Week, false, "hash", Now);

    [Fact]
    public async Task Follows_The_Sites_Own_Redirect_Onto_The_City_Ids_And_Parses_The_Page()
    {
        var handler = new ScriptedHandler(
            Redirect("https://www.kariyer.net/is-ilanlari/istanbul?ct=34,82&kw=.net+developer"),
            Ok(KariyerNetJobSourceFixtures.ThreeCardsWithNextPage));
        var client = Build(handler);

        var result = await client.SearchAsync(Query, 1, CancellationToken.None);

        result.IsOk.ShouldBeTrue();
        result.Value!.Cards.Count.ShouldBe(3);
        result.Value.HasMore.ShouldBeTrue();
        handler.Requests[0].RequestUri!.AbsoluteUri.ShouldBe("https://www.kariyer.net/is-ilanlari/istanbul?kw=.net%20developer");
        handler.Requests[0].Headers.UserAgent.ToString().ShouldStartWith("EKariyerimJobSource/");
        handler.Requests[1].RequestUri!.Query.ShouldContain("ct=34,82");
    }

    [Fact]
    public async Task An_Unknown_City_Lands_On_The_Nationwide_Listing_And_Is_An_Empty_Page()
    {
        var handler = new ScriptedHandler(
            Redirect("https://www.kariyer.net/is-ilanlari?kw=.net+developer"),
            Ok(KariyerNetJobSourceFixtures.ThreeCardsWithNextPage));
        var client = Build(handler);

        var result = await client.SearchAsync(Query, 1, CancellationToken.None);

        result.IsOk.ShouldBeTrue();
        result.Value!.Cards.ShouldBeEmpty();
        result.Value.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task Remote_Only_Keeps_The_Remote_Cards()
    {
        var handler = new ScriptedHandler(Ok(KariyerNetJobSourceFixtures.ThreeCardsWithNextPage));
        var remoteQuery = JobSourceQuery.Create(Source.KariyerNet, ".net developer", "istanbul", JobSourceTimeWindow.Week, true, "hash2", Now);

        var result = await Build(handler).SearchAsync(remoteQuery, 1, CancellationToken.None);

        result.Value!.Cards.Select(c => c.ExternalId).ShouldBe(["4460000002"]);
    }

    [Fact]
    public async Task The_Login_Page_And_An_Off_Site_Redirect_Are_A_Stop()
    {
        (await Build(new ScriptedHandler(Redirect("https://www.kariyer.net/aday/giris?returnUrl=%2Fis-ilanlari")))
            .SearchAsync(Query, 1, CancellationToken.None)).Outcome.ShouldBe(JobSourceFetchOutcome.Blocked);
        (await Build(new ScriptedHandler(Redirect("https://cdn.example.net/challenge")))
            .GetPostingAsync("1", CancellationToken.None)).Outcome.ShouldBe(JobSourceFetchOutcome.Blocked);
        (await Build(new ScriptedHandler(Status(HttpStatusCode.TooManyRequests)))
            .GetPostingAsync("1", CancellationToken.None)).StopsSweep.ShouldBeTrue();
        (await Build(new ScriptedHandler(Status(HttpStatusCode.Forbidden)))
            .GetPostingAsync("1", CancellationToken.None)).Outcome.ShouldBe(JobSourceFetchOutcome.Blocked);
    }

    [Fact]
    public async Task A_Posting_Is_Fetched_By_Id_Through_The_Canonical_Redirect()
    {
        var handler = new ScriptedHandler(
            Redirect("https://www.kariyer.net/is-ilani/acme-yazilim-a-s-software-developer-net-4460000001"),
            Ok(KariyerNetJobSourceFixtures.Posting(KariyerNetJobSourceFixtures.RichDescription)));

        var result = await Build(handler).GetPostingAsync("4460000001", CancellationToken.None);

        result.IsOk.ShouldBeTrue();
        result.Value!.Seniority.ShouldBe("En az 7 yıl tecrübeli");
        handler.Requests[0].RequestUri!.AbsoluteUri.ShouldBe("https://www.kariyer.net/is-ilani/4460000001");
    }

    private static IKariyerNetJobSourceClient Build(HttpMessageHandler handler)
    {
        var options = new JobSourceOptions { RetryBaseDelayMs = 1, AttemptTimeoutSeconds = 5, TotalTimeoutSeconds = 20 };
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(options));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddHttpClient<IKariyerNetJobSourceClient, KariyerNetJobSourceClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddResilienceHandler("test", pipeline => JobSourceResilience.Configure(pipeline, options));
        return services.BuildServiceProvider().GetRequiredService<IKariyerNetJobSourceClient>();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static Func<HttpResponseMessage> Ok(string body) =>
        () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static Func<HttpResponseMessage> Status(HttpStatusCode status) => () => new HttpResponseMessage(status);

    private static Func<HttpResponseMessage> Redirect(string location) =>
        () => new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Headers = { Location = new Uri(location) } };

    private sealed class ScriptedHandler(params Func<HttpResponseMessage>[] script) : HttpMessageHandler
    {
        private int _index;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var step = script[Math.Min(_index++, script.Length - 1)];
            return Task.FromResult(step());
        }
    }
}
