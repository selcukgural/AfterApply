using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CandidateExperiences.Validators;
using AfterApply.Application.Localization;
using AfterApply.Domain.CandidateExperiences;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.CandidateExperiences;

public class CandidateExperienceValidatorTests
{
    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private static CandidateExperienceRequestValidator Validator() => new(new KeyEchoLocalizer());

    private static CandidateExperienceRequest Request(int overall = 4,
        IReadOnlyList<ExperienceCategoryRatingDto>? categories = null, IReadOnlyList<string>? liked = null,
        IReadOnlyList<string>? improvable = null, HiringOutcome? outcome = HiringOutcome.Offer,
        ProcessDuration? duration = ProcessDuration.OneToTwoWeeks, StageCount? stages = StageCount.Two,
        IReadOnlyList<InterviewType>? types = null) =>
        new(overall, categories ?? [new ExperienceCategoryRatingDto(ExperienceCategory.ResponseTime, 5)],
            liked ?? ["response.pos.quick_replies"], improvable ?? ["assignment.imp.duration"],
            outcome, duration, stages, types ?? [InterviewType.Phone, InterviewType.OnSite]);

    [Fact]
    public void Accepts_A_Complete_Request()
    {
        Validator().Validate(Request()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Accepts_A_Bare_Overall_Rating()
    {
        // The minimum entry: one number, nothing else.
        Validator().Validate(new CandidateExperienceRequest(3)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Rejects_A_Rating_Off_The_Scale(int overall)
    {
        var result = Validator().Validate(Request(overall: overall));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "OverallRating");
    }

    [Fact]
    public void Rejects_A_Category_Rated_Twice_Or_Off_The_Scale_Or_Named_Overall()
    {
        Validator().Validate(Request(categories: [new(ExperienceCategory.Transparency, 2), new(ExperienceCategory.Transparency, 4)]))
            .Errors.ShouldContain(e => e.PropertyName == "CategoryRatings" && e.ErrorMessage == "VALIDATION_REVIEW_CATEGORY_DUPLICATE");
        Validator().Validate(Request(categories: [new(ExperienceCategory.Transparency, 7)]))
            .Errors.ShouldContain(e => e.PropertyName.StartsWith("CategoryRatings"));
        Validator().Validate(Request(categories: [new(ExperienceCategory.Overall, 4)]))
            .Errors.ShouldContain(e => e.PropertyName.StartsWith("CategoryRatings"));
    }

    [Fact]
    public void Rejects_Too_Many_Duplicate_Unknown_Or_Wrong_Kind_Statements()
    {
        var six = ExperienceStatementCatalogue.All
            .Where(s => s.Kind == AfterApply.Domain.CompanyReviews.ReviewStatementKind.Liked).Take(6).Select(s => s.Key).ToList();
        Validator().Validate(Request(liked: six))
            .Errors.ShouldContain(e => e.PropertyName == "LikedStatements" && e.ErrorMessage == "VALIDATION_REVIEW_STATEMENTS_TOO_MANY");
        Validator().Validate(Request(liked: ["response.pos.quick_replies", "response.pos.quick_replies"]))
            .Errors.ShouldContain(e => e.PropertyName == "LikedStatements" && e.ErrorMessage == "VALIDATION_REVIEW_STATEMENTS_DUPLICATE");
        Validator().Validate(Request(improvable: ["assignment.imp.made_up"]))
            .Errors.ShouldContain(e => e.PropertyName.StartsWith("ImprovableStatements") && e.ErrorMessage == "VALIDATION_REVIEW_STATEMENT_UNKNOWN");
        Validator().Validate(Request(improvable: ["response.pos.quick_replies"]))
            .Errors.ShouldContain(e => e.PropertyName.StartsWith("ImprovableStatements") && e.ErrorMessage == "VALIDATION_REVIEW_STATEMENT_UNKNOWN");
    }

    [Fact]
    public void Rejects_An_Undefined_Fact_But_Accepts_A_Missing_One()
    {
        Validator().Validate(Request(outcome: (HiringOutcome)42)).Errors.ShouldContain(e => e.PropertyName == "Outcome");
        Validator().Validate(Request(duration: (ProcessDuration)42)).Errors.ShouldContain(e => e.PropertyName == "Duration");
        Validator().Validate(Request(stages: (StageCount)42)).Errors.ShouldContain(e => e.PropertyName == "Stages");
        Validator().Validate(Request(outcome: null, duration: null, stages: null)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_A_Repeated_Or_Undefined_Interview_Type()
    {
        Validator().Validate(Request(types: [InterviewType.Video, InterviewType.Video]))
            .Errors.ShouldContain(e => e.PropertyName == "InterviewTypes" && e.ErrorMessage == "VALIDATION_EXPERIENCE_INTERVIEW_TYPES_DUPLICATE");
        Validator().Validate(Request(types: [(InterviewType)42]))
            .Errors.ShouldContain(e => e.PropertyName.StartsWith("InterviewTypes"));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void List_Page_Is_Bounded(int page, bool valid)
    {
        new CandidateExperienceListQueryValidator().Validate(new CandidateExperienceListQuery(page)).IsValid.ShouldBe(valid);
    }
}
