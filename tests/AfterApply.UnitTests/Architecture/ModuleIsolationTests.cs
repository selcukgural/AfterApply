using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace AfterApply.UnitTests.Architecture;

public class ModuleIsolationTests
{
    private static readonly Assembly DomainAssembly = Assembly.Load("AfterApply.Domain");

    [Fact]
    public void Companies_Should_Not_Depend_On_Applications_Or_Jobs()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("AfterApply.Domain.Companies")
            .Should()
            .NotHaveDependencyOnAny("AfterApply.Domain.Applications", "AfterApply.Domain.Jobs")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Jobs_Should_Not_Depend_On_Applications()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("AfterApply.Domain.Jobs")
            .Should()
            .NotHaveDependencyOn("AfterApply.Domain.Applications")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Occupations_Should_Not_Depend_On_CompanySalaries()
    {
        // The catalogue is generic (a later form may pick from it); salaries depend on it, never
        // the other way round.
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("AfterApply.Domain.Occupations")
            .Should()
            .NotHaveDependencyOn("AfterApply.Domain.CompanySalaries")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Blog_Should_Not_Depend_On_Any_Other_Module()
    {
        // The slug generator is a copy of the companies' one, not a reference to it — this is
        // the test that keeps that so.
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("AfterApply.Domain.Blog")
            .Should()
            .NotHaveDependencyOnAny("AfterApply.Domain.Companies", "AfterApply.Domain.CompanyReviews",
                "AfterApply.Domain.Applications", "AfterApply.Domain.Jobs", "AfterApply.Domain.Documents")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}
