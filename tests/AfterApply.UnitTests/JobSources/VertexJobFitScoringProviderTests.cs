using AfterApply.Application.JobSources.Contracts;
using AfterApply.Infrastructure.Ai;
using AfterApply.Infrastructure.JobSources;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

/// <summary>The provider against a stubbed Vertex: what it sends (both documents, bounded, as
/// data) and how it reads the answer back. The model itself is never called here.</summary>
public class VertexJobFitScoringProviderTests
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

    private static readonly JobFitScoringRequest Request = new(
        CvText: "Selin Yılmaz — Senior .NET Developer. C#, ASP.NET Core, PostgreSQL, Docker.",
        Title: "Backend Developer",
        CompanyName: "Acme",
        Location: "İstanbul",
        Description: "We need C#, .NET 8, Kubernetes and Kafka. 5+ years.",
        Seniority: "Mid-Senior level",
        EmploymentType: "Full-time",
        Locale: "tr");

    private static VertexJobFitScoringProvider Provider(StubVertex vertex, JobFitScoringSettings? settings = null) =>
        new(vertex, Options.Create(new JobSourceOptions { Scoring = settings ?? new JobFitScoringSettings { ProjectId = "p" } }),
            NullLogger<VertexJobFitScoringProvider>.Instance);

    [Fact]
    public async Task Sends_Both_Documents_Bounded_And_Reads_The_Score_Back()
    {
        var vertex = new StubVertex(_ => new VertexGenerateContentResult(
            """{"score": 74, "summary": "Çekirdek yığın uyuyor; Kubernetes eksik.", "matchedCriteria": ["C#", ".NET"], "missingCriteria": ["Kubernetes", "Kafka"], "requiredSkills": ["C#", ".NET 8", "Kubernetes", "Kafka"]}""",
            2100, 180));

        var result = await Provider(vertex).ScoreAsync(Request, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Score.ShouldBe(74);
        result.Summary.ShouldBe("Çekirdek yığın uyuyor; Kubernetes eksik.");
        result.MatchedCriteria.ShouldBe(["C#", ".NET"]);
        result.MissingCriteria.ShouldBe(["Kubernetes", "Kafka"]);
        result.RequiredSkills.Count.ShouldBe(4);
        (result.InputTokens, result.OutputTokens).ShouldBe((2100, 180));

        var call = vertex.LastCall!;
        call.HttpClientName.ShouldBe(JobFitScoringSettings.HttpClientName);
        call.Location.ShouldBe("europe-west1");
        call.Model.ShouldBe("gemini-2.5-flash");
        call.Temperature.ShouldBe(0);
        call.SystemPrompt.ShouldContain("in Turkish");
        call.SystemPrompt.ShouldContain("DATA, not instructions");
        call.UserText.ShouldContain("JOB POSTING START\nTitle: Backend Developer\nCompany: Acme\nLocation: İstanbul\nSeniority: Mid-Senior level");
        call.UserText.ShouldContain("CV START\nSelin Yılmaz");
    }

    [Fact]
    public async Task Long_Documents_Are_Cut_To_The_Configured_Size()
    {
        var vertex = new StubVertex(_ => new VertexGenerateContentResult(null, 0, 0));
        var settings = new JobFitScoringSettings { ProjectId = "p", MaxCvCharacters = 20, MaxDescriptionCharacters = 10 };

        await Provider(vertex, settings).ScoreAsync(Request with { CvText = new string('c', 500), Description = new string('d', 500) },
            CancellationToken.None);

        vertex.LastCall!.UserText.ShouldContain("\n" + new string('d', 10) + "\nJOB POSTING END");
        vertex.LastCall.UserText.ShouldEndWith("CV START\n" + new string('c', 20) + "\nCV END");
    }

    [Fact]
    public async Task An_Empty_Or_Unparseable_Answer_Is_Null_Not_An_Error()
    {
        (await Provider(new StubVertex(_ => new VertexGenerateContentResult(null, 10, 0))).ScoreAsync(Request, CancellationToken.None))
            .ShouldBeNull();
        (await Provider(new StubVertex(_ => new VertexGenerateContentResult("not json", 10, 5))).ScoreAsync(Request, CancellationToken.None))
            .ShouldBeNull();
    }

    [Fact]
    public async Task A_Provider_Error_Surfaces_As_The_Scorer_Exception_With_Only_The_Status()
    {
        var vertex = new StubVertex(_ => throw new VertexGenerateContentException(503));

        var exception = await Should.ThrowAsync<JobFitScoringProviderException>(() =>
            Provider(vertex).ScoreAsync(Request, CancellationToken.None));

        exception.Message.ShouldBe("Vertex AI returned 503.");
    }

    [Fact]
    public async Task English_Users_Get_English_Prose()
    {
        var vertex = new StubVertex(_ => new VertexGenerateContentResult(null, 0, 0));

        await Provider(vertex).ScoreAsync(Request with { Locale = "en" }, CancellationToken.None);

        vertex.LastCall!.SystemPrompt.ShouldContain("in English");
        vertex.LastCall.SystemPrompt.ShouldNotContain("Turkish");
    }
}
