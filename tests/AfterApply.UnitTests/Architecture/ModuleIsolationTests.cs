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
}
