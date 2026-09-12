using AfterApply.Application.JobSources.Contracts;
using AfterApply.Application.JobSources.Validators;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

public class JobSourceValidatorTests
{
    private readonly UpsertJobSourceProfileRequestValidator _profile = new();

    [Fact]
    public void One_To_Three_Titles_And_A_Location()
    {
        _profile.Validate(new UpsertJobSourceProfileRequest([".NET Developer"], "İstanbul")).IsValid.ShouldBeTrue();
        _profile.Validate(new UpsertJobSourceProfileRequest(["a", "b", "c"], "İstanbul")).IsValid.ShouldBeFalse(); // too short
        _profile.Validate(new UpsertJobSourceProfileRequest(["aa", "bb", "cc"], "İstanbul")).IsValid.ShouldBeTrue();
        _profile.Validate(new UpsertJobSourceProfileRequest(["aa", "bb", "cc", "dd"], "İstanbul")).IsValid.ShouldBeFalse();
        _profile.Validate(new UpsertJobSourceProfileRequest([], "İstanbul")).IsValid.ShouldBeFalse();
        _profile.Validate(new UpsertJobSourceProfileRequest([".NET Developer"], "")).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Control_Characters_Are_Refused()
    {
        _profile.Validate(new UpsertJobSourceProfileRequest([".NET\nDeveloper"], "İstanbul")).IsValid.ShouldBeFalse();
        _profile.Validate(new UpsertJobSourceProfileRequest([".NET\u0007Developer"], "İstanbul")).IsValid.ShouldBeFalse();
        _profile.Validate(new UpsertJobSourceProfileRequest([".NET Developer"], "İstanbul\u001b[0m")).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Length_Caps_Match_The_Query_Columns()
    {
        _profile.Validate(new UpsertJobSourceProfileRequest([new string('x', 101)], "İstanbul")).IsValid.ShouldBeFalse();
        _profile.Validate(new UpsertJobSourceProfileRequest(["Dev"], new string('x', 101))).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Limits_And_Entitlements()
    {
        var limits = new UpdateUserJobSourceLimitsRequestValidator();
        limits.Validate(new UpdateUserJobSourceLimitsRequest(null)).IsValid.ShouldBeTrue();
        limits.Validate(new UpdateUserJobSourceLimitsRequest(0)).IsValid.ShouldBeTrue();
        limits.Validate(new UpdateUserJobSourceLimitsRequest(501)).IsValid.ShouldBeFalse();
        limits.Validate(new UpdateUserJobSourceLimitsRequest(-1)).IsValid.ShouldBeFalse();

        var grant = new GrantProEntitlementRequestValidator();
        grant.Validate(new GrantProEntitlementRequest(DateTimeOffset.UtcNow.AddMonths(1))).IsValid.ShouldBeTrue();
        grant.Validate(new GrantProEntitlementRequest(DateTimeOffset.UtcNow.AddDays(-1))).IsValid.ShouldBeFalse();
    }
}
