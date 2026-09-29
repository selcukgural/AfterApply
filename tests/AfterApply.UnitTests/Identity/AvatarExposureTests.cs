using System.Reflection;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

/// <summary>
/// The profile photo reaches exactly the places DECISIONS.md 2026-09-28 names: the owner's own
/// profile, a blog comment, and the admin's comment moderation. Salaries, company reviews,
/// candidate experiences, silence reports and response rates are anonymous, and a photo next to
/// any of them would undo that — so a new avatar field anywhere else fails here, and widening the
/// list is a decision to record, not a line to add.
/// </summary>
public class AvatarExposureTests
{
    private static readonly Assembly ApplicationAssembly = typeof(IAvatarService).Assembly;

    private static readonly HashSet<Type> AllowedToCarryAnAvatar =
    [
        typeof(UserProfileResponse),
        typeof(UpdateAvatarVisibilityRequest),
        typeof(BlogCommentResponse),
        typeof(AdminBlogCommentListItemResponse),
        typeof(AvatarContent)
    ];

    [Fact]
    public void Only_The_Allowed_Contracts_Carry_An_Avatar_Field()
    {
        var carriers = ApplicationAssembly.GetTypes()
            .Where(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(property => property.Name.Contains("Avatar", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        carriers.Where(type => !AllowedToCarryAnAvatar.Contains(type)).Select(type => type.FullName).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("AfterApply.Application.CompanySalaries")]
    [InlineData("AfterApply.Application.CompanyReviews")]
    [InlineData("AfterApply.Application.CandidateExperiences")]
    [InlineData("AfterApply.Application.SilenceReports")]
    [InlineData("AfterApply.Application.ResponseRates")]
    [InlineData("AfterApply.Application.CompanyIntelligence")]
    public void No_Anonymous_Surface_Holds_The_Whole_Profile_Either(string namespacePrefix)
    {
        // The other way a photo could leak: a sensitive contract embedding the profile record itself.
        var embedding = ApplicationAssembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true)
            .Where(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(property => property.PropertyType == typeof(UserProfileResponse)))
            .Select(type => type.FullName)
            .ToList();

        embedding.ShouldBeEmpty();
    }
}
