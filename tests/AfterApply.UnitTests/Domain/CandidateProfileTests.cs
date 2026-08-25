using AfterApply.Domain.Matching;
using Shouldly;

namespace AfterApply.UnitTests.Domain;

public class CandidateProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_Sets_UserId_And_CvText()
    {
        var userId = Guid.CreateVersion7();

        var profile = CandidateProfile.Create(userId, "C# / .NET / PostgreSQL", Now);

        profile.UserId.ShouldBe(userId);
        profile.CvText.ShouldBe("C# / .NET / PostgreSQL");
        profile.CreatedAt.ShouldBe(Now);
        profile.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public void UpdateCv_Replaces_Text_And_Touches_UpdatedAt()
    {
        var profile = CandidateProfile.Create(Guid.CreateVersion7(), "Old CV text", Now);
        var later = Now.AddDays(1);

        profile.UpdateCv("New CV text", later);

        profile.CvText.ShouldBe("New CV text");
        profile.CreatedAt.ShouldBe(Now);
        profile.UpdatedAt.ShouldBe(later);
    }
}
