using System.Reflection;

namespace AfterApply.UnitTests.JobSearch;

/// <summary>Canned provider bodies from tests/Fixtures/JobSearch, embedded at build.</summary>
internal static class JSearchFixtures
{
    public static string Read(string name)
    {
        var resource = $"Fixtures.JobSearch.{name}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                           ?? throw new InvalidOperationException($"Missing embedded fixture {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
