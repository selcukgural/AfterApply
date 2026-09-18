using System.Reflection;
using Shouldly;

namespace AfterApply.UnitTests.Architecture;

/// <summary>
/// The inverse of the guard that stood here from 2026-09-06 to 2026-09-18. Then, the L1-only
/// cache was deliberate and this test forbade any Redis reference. Now the API runs as several
/// Cloud Run instances, and a cache whose invalidations stop at the instance boundary is the bug
/// that brought Redis back (DECISIONS.md 2026-09-18): the summary a candidate experience updated
/// on one instance stayed stale on every other. So the guard points the other way — Infrastructure
/// must carry the backplane, because dropping it is a one-line change that would look harmless in
/// review and would silently reintroduce that bug.
/// </summary>
public class CachingDependencyTests
{
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("AfterApply.Infrastructure");

    [Theory]
    [InlineData("ZiggyCreatures.FusionCache")]
    [InlineData("ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis")]
    [InlineData("Microsoft.Extensions.Caching.StackExchangeRedis")]
    public void Infrastructure_Should_Reference_The_Cache_Backplane(string assemblyName)
    {
        var references = InfrastructureAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        references.ShouldContain(assemblyName,
            $"AfterApply.Infrastructure no longer references {assemblyName}. With more than one Cloud Run " +
            "instance the cache needs a shared L2 and a backplane, or an eviction on one instance never " +
            "reaches the others — see DECISIONS.md 2026-09-18.");
    }

    /// <summary>Microsoft's own DefaultHybridCache has no backplane; if it came back beside
    /// FusionCache the services would resolve whichever registration won, and the losing one
    /// would be the one that propagates evictions.</summary>
    [Fact]
    public void Infrastructure_Should_Not_Reference_Microsofts_HybridCache_Implementation()
    {
        var references = InfrastructureAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        references.ShouldNotContain("Microsoft.Extensions.Caching.Hybrid",
            "The HybridCache abstract class lives in Microsoft.Extensions.Caching.Abstractions; the " +
            "Microsoft.Extensions.Caching.Hybrid package is the implementation FusionCache replaces.");
    }
}
