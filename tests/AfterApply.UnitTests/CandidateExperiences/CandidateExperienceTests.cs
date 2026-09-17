using AfterApply.Domain.CandidateExperiences;
using Shouldly;

namespace AfterApply.UnitTests.CandidateExperiences;

public class CandidateExperienceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    private static CandidateExperienceContent Content(int overall = 4,
        IReadOnlyList<ExperienceCategoryRating>? categories = null,
        IReadOnlyList<string>? liked = null, IReadOnlyList<string>? improvable = null,
        HiringOutcome? outcome = HiringOutcome.Rejected, ProcessDuration? duration = ProcessDuration.TwoToFourWeeks,
        StageCount? stages = StageCount.Three, IReadOnlyList<InterviewType>? types = null) =>
        new(overall, categories ?? [new(ExperienceCategory.Communication, 5)],
            liked ?? ["communication.pos.steps_clear_upfront"], improvable ?? ["outcome.imp.notification"],
            outcome, duration, stages, types ?? [InterviewType.Video, InterviewType.TakeHomeAssignment]);

    [Fact]
    public void An_Overall_Rating_Alone_Is_Enough()
    {
        var experience = CandidateExperience.Create(Guid.NewGuid(), Guid.NewGuid(),
            new CandidateExperienceContent(3, [], [], [], null, null, null, []), Now);

        experience.OverallRating.ShouldBe(3);
        experience.Outcome.ShouldBeNull();
        experience.Duration.ShouldBeNull();
        experience.Stages.ShouldBeNull();
        experience.SubmittedAt.ShouldBe(Now);
    }

    [Fact]
    public void Create_Keeps_Every_Fact()
    {
        var experience = CandidateExperience.Create(Guid.NewGuid(), Guid.NewGuid(), Content(), Now);

        experience.Outcome.ShouldBe(HiringOutcome.Rejected);
        experience.Duration.ShouldBe(ProcessDuration.TwoToFourWeeks);
        experience.Stages.ShouldBe(StageCount.Three);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Overall_Outside_The_Scale_Is_Refused(int overall)
    {
        Should.Throw<CandidateExperienceContentInvalidException>(() => Content(overall: overall).Validate());
    }

    [Fact]
    public void Overall_As_A_Category_Row_Is_Refused()
    {
        Should.Throw<CandidateExperienceContentInvalidException>(() =>
            Content(categories: [new(ExperienceCategory.Overall, 4)]).Validate());
    }

    [Fact]
    public void A_Category_Rated_Twice_Or_Off_The_Scale_Is_Refused()
    {
        Should.Throw<CandidateExperienceContentInvalidException>(() =>
            Content(categories: [new(ExperienceCategory.Punctuality, 4), new(ExperienceCategory.Punctuality, 2)]).Validate());
        Should.Throw<CandidateExperienceContentInvalidException>(() =>
            Content(categories: [new(ExperienceCategory.Punctuality, 0)]).Validate());
    }

    [Fact]
    public void A_Sixth_Pick_Of_A_Kind_Is_Refused()
    {
        var six = ExperienceStatementCatalogue.All
            .Where(s => s.Kind == AfterApply.Domain.CompanyReviews.ReviewStatementKind.Liked).Take(6).Select(s => s.Key).ToList();
        Should.Throw<CandidateExperienceContentInvalidException>(() => Content(liked: six).Validate());
    }

    [Fact]
    public void A_Liked_Key_In_The_Improve_List_Is_Refused()
    {
        Should.Throw<CandidateExperienceContentInvalidException>(() =>
            Content(improvable: ["communication.pos.steps_clear_upfront"]).Validate());
    }

    [Fact]
    public void An_Unknown_Or_Repeated_Key_Is_Refused()
    {
        Should.Throw<CandidateExperienceContentInvalidException>(() => Content(liked: ["general.pos.made_up"]).Validate());
        Should.Throw<CandidateExperienceContentInvalidException>(() =>
            Content(liked: ["general.pos.clear_process", "general.pos.clear_process"]).Validate());
    }

    [Fact]
    public void An_Undefined_Fact_Is_Refused()
    {
        Should.Throw<CandidateExperienceContentInvalidException>(() => Content(outcome: (HiringOutcome)99).Validate());
        Should.Throw<CandidateExperienceContentInvalidException>(() => Content(duration: (ProcessDuration)99).Validate());
        Should.Throw<CandidateExperienceContentInvalidException>(() => Content(stages: (StageCount)99).Validate());
        Should.Throw<CandidateExperienceContentInvalidException>(() => Content(types: [(InterviewType)99]).Validate());
    }

    [Fact]
    public void A_Repeated_Interview_Type_Is_Refused()
    {
        Should.Throw<CandidateExperienceContentInvalidException>(() =>
            Content(types: [InterviewType.Panel, InterviewType.Panel]).Validate());
    }

    [Fact]
    public void Edit_Replaces_The_Facts_And_Resets_The_Submission_Date()
    {
        var experience = CandidateExperience.Create(Guid.NewGuid(), Guid.NewGuid(), Content(), Now);
        var later = Now.AddDays(40);

        experience.Edit(Content(overall: 2, outcome: HiringOutcome.NoResponse, duration: null, stages: StageCount.One), later);

        experience.OverallRating.ShouldBe(2);
        experience.Outcome.ShouldBe(HiringOutcome.NoResponse);
        experience.Duration.ShouldBeNull();
        experience.Stages.ShouldBe(StageCount.One);
        experience.SubmittedAt.ShouldBe(later);
        experience.UpdatedAt.ShouldBe(later);
        experience.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Statements_Come_Back_In_The_Authors_Order()
    {
        Content(liked: ["general.pos.clear_process", "communication.pos.timely_information"], improvable: ["outcome.imp.notification"])
            .Statements().Select(s => s.Key)
            .ShouldBe(["general.pos.clear_process", "communication.pos.timely_information", "outcome.imp.notification"]);
    }
}
