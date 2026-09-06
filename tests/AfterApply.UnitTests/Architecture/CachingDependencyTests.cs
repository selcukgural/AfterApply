using System.Reflection;
using Shouldly;

namespace AfterApply.UnitTests.Architecture;

/// <summary>
/// Redis was removed in 2026-09-06 (DECISIONS.md): every HybridCacheEntryOptions in the codebase
/// sets LocalCacheExpiration equal to Expiration and nothing wires a backplane, so the L2 never
/// changed an outcome — it only carried a Memorystore bill. These guard the removal staying
/// removed, because re-adding it is a one-line change that would look harmless in review and
/// would silently re-introduce a paid dependency the design does not use.
/// </summary>
public class CachingDependencyTests
{
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("AfterApply.Infrastructure");
    private static readonly Assembly ApiAssembly = Assembly.Load("AfterApply.Api");

    [Theory]
    [InlineData("AfterApply.Infrastructure")]
    [InlineData("AfterApply.Api")]
    public void No_Assembly_Should_Reference_Redis(string assemblyName)
    {
        var assembly = assemblyName == "AfterApply.Infrastructure" ? InfrastructureAssembly : ApiAssembly;

        var redisReferences = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.Contains("Redis", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        redisReferences.ShouldBeEmpty(
            $"{assemblyName} references {string.Join(", ", redisReferences)}. HybridCache is deliberately " +
            "L1-only; adding a distributed cache back is a deployment and cost decision, not a code one.");
    }
}
