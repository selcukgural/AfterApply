using AfterApply.Application.EmailIntegrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.EmailIntegrations;

/// <summary>Fails app startup (wired via AddOptions&lt;EmailIntelligenceOptions&gt;().ValidateOnStart()
/// in DependencyInjection.AddInfrastructure) if any EmailIntelligence:* value is missing from
/// appsettings.json. EmailIntelligenceOptions/Weights/Phrases carry no C# defaults on purpose — every
/// threshold, weight, and phrase list is meant to be tunable without a code deploy, which only works
/// if a missing value is caught immediately instead of silently binding to 0/null/empty.
///
/// Walks EmailIntelligenceOptions' own property tree via reflection against the raw IConfiguration
/// (not the bound object — an unset int binds to 0, which is indistinguishable from a deliberately
/// configured 0) so a newly added weight/phrase property is automatically covered without updating
/// this class.</summary>
public sealed class EmailIntelligenceConfigurationValidator(IConfiguration configuration) : IValidateOptions<EmailIntelligenceOptions>
{
    private const string RootSectionKey = "EmailIntelligence";

    public ValidateOptionsResult Validate(string? name, EmailIntelligenceOptions options)
    {
        var missing = new List<string>();
        CollectMissingKeys(typeof(EmailIntelligenceOptions), configuration.GetSection(RootSectionKey), RootSectionKey, missing);

        return missing.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Missing required configuration value(s) — set these in appsettings.json: {string.Join(", ", missing)}");
    }

    private static void CollectMissingKeys(Type type, IConfigurationSection section, string path, List<string> missing)
    {
        foreach (var property in type.GetProperties())
        {
            var childPath = $"{path}:{property.Name}";
            var child = section.GetSection(property.Name);

            if (property.PropertyType == typeof(string[]))
            {
                var values = child.Get<string[]>();
                if (values is null || values.Length == 0 || values.Any(string.IsNullOrWhiteSpace))
                {
                    missing.Add(childPath);
                }
            }
            else if (property.PropertyType == typeof(int))
            {
                if (!child.Exists())
                {
                    missing.Add(childPath);
                }
            }
            else
            {
                // Nested options object (Weights/Phrases) — recurse instead of asserting a leaf value.
                CollectMissingKeys(property.PropertyType, child, childPath, missing);
            }
        }
    }
}
