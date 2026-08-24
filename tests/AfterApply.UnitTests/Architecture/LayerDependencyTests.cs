using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace AfterApply.UnitTests.Architecture;

public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = Assembly.Load("AfterApply.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load("AfterApply.Application");
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("AfterApply.Infrastructure");

    [Fact]
    public void Domain_Should_Not_Depend_On_OtherLayers()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny("AfterApply.Application", "AfterApply.Infrastructure", "AfterApply.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_EfCore_Or_AspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny("AfterApply.Infrastructure", "AfterApply.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOn("AfterApply.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}
