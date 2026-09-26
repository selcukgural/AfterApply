using AfterApply.Application.Blog;
using AfterApply.Application.Common;
using AfterApply.Infrastructure.Ai;
using AfterApply.Infrastructure.Blog;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using AfterApply.Domain.Blog;
using Shouldly;

namespace AfterApply.UnitTests.Blog;

/// <summary>The SEO suggestion provider against a stubbed Vertex (2026-09-21): what it sends
/// (the draft's words, bounded, the flags the prompt keys on) and how the answer is cut to the
/// store's shape. The model is never called here.</summary>
public class VertexBlogSeoSuggestionProviderTests
{
    private sealed class StubVertex(Func<VertexGenerateContentCall, VertexGenerateContentResult> answer) : IVertexGenerateContentClient
    {
        public VertexGenerateContentCall? LastCall { get; private set; }

        public Task<VertexGenerateContentResult> GenerateAsync(VertexGenerateContentCall call, CancellationToken cancellationToken)
        {
            LastCall = call;
            return Task.FromResult(answer(call));
        }
    }

    private static readonly BlogSeoSuggestionRequest Request = new(
        Language: "tr",
        Title: "İşe Alım Sürecinde Ghosting: Neden Cevap Alamıyorsunuz?",
        Excerpt: "Başvuruların büyük çoğunluğu yanıtsız kalıyor.",
        BodyText: "Başvuruların yüzde sekseni yanıtsız kalıyor. Red mailleri nadiren neden belirtiyor.",
        LockedSlug: null,
        HasCover: true);

    private static VertexBlogSeoSuggestionProvider Provider(StubVertex vertex, BlogOptions.BlogSeoSettings? settings = null) =>
        new(vertex, Options.Create(new BlogOptions { Seo = settings ?? new BlogOptions.BlogSeoSettings { ProjectId = "p" } }),
            NullLogger<VertexBlogSeoSuggestionProvider>.Instance);

    private const string Answer =
        """
        {"seoTitle": " İşe Alımda Ghosting: Neden Cevap Gelmiyor? ", "metaDescription": "Başvuruların yüzde sekseni yanıtsız. Kim susuyor, ne zaman ve ilk haftadan sonra ne bekleyebilirsiniz.",
         "primaryKeyword": "işe alımda ghosting", "secondaryKeywords": ["İşe Alımda Ghosting", "mülakat sonrası sessizlik", "Mülakat Sonrası Sessizlik", "", "ik geri dönüş süresi"],
         "coverAlt": "Cevapsız başvuruları temsil eden soyut gradyan", "slug": "İşe Alımda Ghosting Neden", "intentNote": "Bilgi arayan aday; ilk H2 doğrudan cevabı versin.",
         "coverHook": "  Sessizlik\n de bir cevap ", "coverIconKeyword": " Bell Off "}
        """;

    [Fact]
    public async Task Sends_The_Draft_As_Data_With_The_Flags_The_Prompt_Keys_On_And_Thinking_Off()
    {
        var vertex = new StubVertex(_ => new VertexGenerateContentResult(Answer, 900, 120));

        await Provider(vertex).SuggestAsync(Request, CancellationToken.None);

        var call = vertex.LastCall!;
        call.HttpClientName.ShouldBe(BlogOptions.BlogSeoSettings.HttpClientName);
        call.Location.ShouldBe("europe-west1");
        call.Model.ShouldBe("gemini-2.5-flash");
        call.ThinkingBudget.ShouldBe(0);
        call.SystemPrompt.ShouldContain("Turkish");
        call.SystemPrompt.ShouldNotContain("{LANGUAGE}");
        call.UserText.ShouldContain("hasCover: true");
        call.UserText.ShouldContain("slugAllowed: true");
        call.UserText.ShouldContain("BODY START\nBaşvuruların yüzde sekseni");
    }

    [Fact]
    public async Task Tells_The_Model_Whether_It_Is_Describing_A_Blog_Post_Or_A_Guide()
    {
        var vertex = new StubVertex(_ => new VertexGenerateContentResult(Answer, 900, 120));

        await Provider(vertex).SuggestAsync(Request, CancellationToken.None);
        vertex.LastCall!.SystemPrompt.ShouldContain("metadata for blog posts");

        await Provider(vertex).SuggestAsync(Request with { Kind = BlogPostKind.Guide }, CancellationToken.None);
        vertex.LastCall!.SystemPrompt.ShouldContain("metadata for guide articles");
        vertex.LastCall.SystemPrompt.ShouldNotContain("{KIND}");
    }

    [Fact]
    public async Task Cuts_The_Answer_To_The_Stores_Shape()
    {
        var vertex = new StubVertex(_ => new VertexGenerateContentResult(Answer, 900, 120));

        var result = await Provider(vertex).SuggestAsync(Request, CancellationToken.None);

        result.SeoTitle.ShouldBe("İşe Alımda Ghosting: Neden Cevap Gelmiyor?");
        result.PrimaryKeyword.ShouldBe("işe alımda ghosting");
        // The primary is not a secondary too; duplicates fold case-insensitively; blanks go.
        result.SecondaryKeywords.ShouldBe(["mülakat sonrası sessizlik", "ik geri dönüş süresi"]);
        result.CoverAlt.ShouldBe("Cevapsız başvuruları temsil eden soyut gradyan");
        // A slug the generator would refuse is generated from what the model said.
        result.Slug.ShouldBe("ise-alimda-ghosting-neden");
        result.IntentNote.ShouldBe("Bilgi arayan aday; ilk H2 doğrudan cevabı versin.");
        // The cover line is one line; the icon keyword is what the editor's search takes.
        result.CoverHook.ShouldBe("Sessizlik de bir cevap");
        result.CoverIconKeyword.ShouldBe("bell off");
    }

    [Fact]
    public async Task Asks_For_A_Cover_Line_And_An_Icon_Keyword()
    {
        var vertex = new StubVertex(_ => new VertexGenerateContentResult(Answer, 900, 120));

        await Provider(vertex).SuggestAsync(Request, CancellationToken.None);

        vertex.LastCall!.SystemPrompt.ShouldContain("coverHook");
        vertex.LastCall.SystemPrompt.ShouldContain("coverIconKeyword");
    }

    [Theory]
    [InlineData("mail", "mail")]
    [InlineData(" Money ", "money")]
    [InlineData("bell-off", "bell off")]
    [InlineData("three word answer", null)]
    [InlineData("<svg>", null)]
    [InlineData("çanta", null)]
    [InlineData("", null)]
    public async Task The_Icon_Keyword_Is_One_Or_Two_Plain_Words(string raw, string? expected)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { seoTitle = "t", coverIconKeyword = raw });
        var vertex = new StubVertex(_ => new VertexGenerateContentResult(json, 10, 5));

        (await Provider(vertex).SuggestAsync(Request, CancellationToken.None)).CoverIconKeyword.ShouldBe(expected);
    }

    [Fact]
    public async Task An_Over_Long_Cover_Line_Is_Cut_To_The_Cap()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { seoTitle = "t", coverHook = new string('a', 80) });
        var vertex = new StubVertex(_ => new VertexGenerateContentResult(json, 10, 5));

        (await Provider(vertex).SuggestAsync(Request, CancellationToken.None)).CoverHook!.Length.ShouldBe(BlogCoverCard.MaxHookLength);
    }

    [Fact]
    public async Task A_Published_Post_Gets_No_Slug_And_A_Post_Without_A_Cover_No_Alt()
    {
        var vertex = new StubVertex(_ => new VertexGenerateContentResult(Answer, 900, 120));

        var locked = Request with { LockedSlug = "ise-alim-surecinde-ghosting", HasCover = false };
        var result = await Provider(vertex).SuggestAsync(locked, CancellationToken.None);

        result.Slug.ShouldBeNull();
        result.CoverAlt.ShouldBeNull();
        vertex.LastCall!.UserText.ShouldContain("slugAllowed: false");
        vertex.LastCall.UserText.ShouldContain("hasCover: false");
    }

    [Fact]
    public async Task Bounds_The_Body_To_The_Input_Cap_And_Speaks_English_For_An_English_Post()
    {
        var vertex = new StubVertex(_ => new VertexGenerateContentResult(Answer, 900, 120));
        var settings = new BlogOptions.BlogSeoSettings { ProjectId = "p", MaxInputCharacters = 20 };

        await Provider(vertex, settings).SuggestAsync(Request with { Language = "en", BodyText = new string('x', 100) }, CancellationToken.None);

        vertex.LastCall!.UserText.ShouldContain($"BODY START\n{new string('x', 20)}\nBODY END");
        vertex.LastCall.SystemPrompt.ShouldContain("English");
    }

    [Fact]
    public async Task Refuses_With_A_Code_When_Unconfigured_Empty_Or_Failing()
    {
        var unconfigured = Provider(new StubVertex(_ => throw new InvalidOperationException("must not be called")),
            new BlogOptions.BlogSeoSettings { ProjectId = "" });
        (await Should.ThrowAsync<CodedException>(() => unconfigured.SuggestAsync(Request, CancellationToken.None)))
            .ErrorCode.ShouldBe("BLOG_SEO_SUGGEST_NOT_CONFIGURED");

        var empty = Provider(new StubVertex(_ => new VertexGenerateContentResult(null, 10, 0, "MAX_TOKENS")));
        (await Should.ThrowAsync<CodedException>(() => empty.SuggestAsync(Request, CancellationToken.None)))
            .ErrorCode.ShouldBe("BLOG_SEO_SUGGEST_EMPTY");

        var garbled = Provider(new StubVertex(_ => new VertexGenerateContentResult("not json", 10, 5)));
        (await Should.ThrowAsync<CodedException>(() => garbled.SuggestAsync(Request, CancellationToken.None)))
            .ErrorCode.ShouldBe("BLOG_SEO_SUGGEST_EMPTY");

        var failing = Provider(new StubVertex(_ => throw new VertexGenerateContentException(503)));
        (await Should.ThrowAsync<CodedException>(() => failing.SuggestAsync(Request, CancellationToken.None)))
            .ErrorCode.ShouldBe("BLOG_SEO_SUGGEST_FAILED");
    }
}
