using System.Globalization;
using System.Text.RegularExpressions;

namespace AfterApply.Application.SalaryMarket.Import;

/// <summary>
/// One answer as a survey file holds it, before anything is decided about it. Every field is the
/// survey's own text; <see cref="Currency"/> is null for the early years that only asked in TRY.
/// Gender, technologies and company type are deliberately not carried: nothing published uses
/// them, and a field that never leaves the reader cannot leak.
/// </summary>
public sealed record SurveyResponse(int Year, string SourceCode, string? Position, string? Level, string? Experience,
    string? City, string? Currency, string? Salary);

/// <summary>What an answer counts as once normalised, or why it does not count.</summary>
public sealed record NormalizedResponse(int Year, string SourceCode, string Group, SalaryMarketLevel? Level,
    SalaryMarketExperience? Experience, SalaryBand Salary);

public enum SurveyExclusion
{
    /// <summary>Paid in another currency — a TRY percentile cannot hold it.</summary>
    ForeignCurrency,

    /// <summary>Works abroad ("Yurt Dışı", "* Almanya"): not the Turkish market the pages describe.</summary>
    Abroad,

    NoSalary,

    /// <summary>A position that is none of the published occupations ("Diğer", "UI/UX Designer").</summary>
    UnknownPosition
}

/// <summary>
/// Folds a survey answer onto the published scales: an occupation (<see cref="SalaryMarketGroups"/>),
/// one of three levels, one of four experience ranges and a salary value. Rules, in order, are
/// the ones checked by hand against the 2018–2026 files on 2026-09-27 (DECISIONS.md): 232 raw
/// position labels, every one with at least 15 answers mapped, the rest is noise ("IT", "Consultant").
/// </summary>
public static class SalarySurveyNormalizer
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    // First match wins, so the narrow rules come before the broad ones: "Mobile Application
    // Developer (Full Stack)" is mobile, not full stack; "Director of Software Development" is a
    // director although it contains "cto"; "Support Engineer" is not a product owner although it
    // contains "po". Patterns are matched on the label lower-cased invariantly: the labels are
    // English, and Turkish casing would turn "AI" into "aı".
    private static readonly (string Group, Regex Pattern)[] PositionRules =
    [
        ("full-stack-developer", Rx(@"^full stack developer$")),
        ("mobile-developer", Rx(@"mobile")),
        ("back-end-developer", Rx(@"back-?end")),
        ("front-end-developer", Rx(@"front-?end")),
        ("embedded-software-developer", Rx(@"embedded")),
        // Cloud and platform engineers sit with DevOps in the catalogue (EK-0018, EK-0019) too.
        ("devops-engineer", Rx(@"devops|site reliability|\bsre\b|\bcloud\b|platform engineer")),
        ("qa-engineer", Rx(@"\bqa\b|test")),
        ("ai-engineer", Rx(@"\bai (engineer|developer)|artificial")),
        ("data-scientist", Rx(@"data scientist|machine learning|computer vision")),
        ("data-analyst", Rx(@"data analyst")),
        ("data-engineer", Rx(@"data engineer")),
        ("game-developer", Rx(@"\bgame")),
        ("software-architect", Rx(@"architect")),
        ("team-lead", Rx(@"team lead|tech lead")),
        ("software-director", Rx(@"director")),
        ("engineering-manager", Rx(@"^cto / software development manager$")),
        ("cto", Rx(@"\bcto\b")),
        ("engineering-manager", Rx(@"development manager|engineering manager")),
        ("product-manager", Rx(@"product|\bpo\b")),
        ("project-manager", Rx(@"project manager")),
        ("business-analyst", Rx(@"analyst")),
        ("sap-erp-developer", Rx(@"\bsap\b|abap|\berp\b")),
        ("database-administrator", Rx(@"\bdba\b|database")),
        ("cyber-security", Rx(@"cyber|security")),
        ("system-engineer", Rx(@"system|sysadmin|support|network")),
    ];

    private static readonly Regex FirstNumber = new(@"\d+", RegexOptions.CultureInvariant);

    public static (NormalizedResponse? Response, SurveyExclusion? Exclusion) Normalize(SurveyResponse answer)
    {
        if (!IsTry(answer.Currency))
        {
            return (null, SurveyExclusion.ForeignCurrency);
        }

        if (IsAbroad(answer.City))
        {
            return (null, SurveyExclusion.Abroad);
        }

        var salary = SalaryBand.Parse(answer.Salary);
        if (salary is null)
        {
            return (null, SurveyExclusion.NoSalary);
        }

        var group = GroupOf(answer.Position);
        if (group is null)
        {
            return (null, SurveyExclusion.UnknownPosition);
        }

        return (new NormalizedResponse(answer.Year, answer.SourceCode, group, LevelOf(answer.Level),
            ExperienceOf(answer.Experience), salary), null);
    }

    public static string? GroupOf(string? position)
    {
        if (string.IsNullOrWhiteSpace(position))
        {
            return null;
        }

        var label = position.Trim().ToLowerInvariant();
        foreach (var (group, pattern) in PositionRules)
        {
            if (pattern.IsMatch(label))
            {
                return group;
            }
        }

        return null;
    }

    public static SalaryMarketLevel? LevelOf(string? level) =>
        level?.Trim().ToLowerInvariant() switch
        {
            "junior" => SalaryMarketLevel.Junior,
            "middle" or "mid" or "mid-level" => SalaryMarketLevel.Middle,
            // "Guru" (yazilimcimaaslari.org) sits above senior on its own scale; the pages have no
            // fourth step, and it is closer to senior than to anything else.
            "senior" or "guru" => SalaryMarketLevel.Senior,
            _ => null
        };

    /// <summary>By the first number of the range: "0 - 1", "1 - 3" and "0-2" are the first
    /// step, "3 - 5" the second, "5 - 7", "7 - 10" and "6-10" the third, anything from ten up
    /// ("10 - 12", "15 Yıl ve üzeri", "10 yıldan daha fazla") the last.</summary>
    public static SalaryMarketExperience? ExperienceOf(string? experience)
    {
        var match = experience is null ? null : FirstNumber.Match(experience);
        if (match is null || !match.Success)
        {
            return null;
        }

        var years = int.Parse(match.Value, CultureInfo.InvariantCulture);
        return years switch
        {
            <= 1 => SalaryMarketExperience.ZeroToTwo,
            <= 4 => SalaryMarketExperience.ThreeToFive,
            <= 9 => SalaryMarketExperience.SixToTen,
            _ => SalaryMarketExperience.TenPlus
        };
    }

    private static bool IsTry(string? currency)
    {
        if (currency is null)
        {
            return true;
        }

        var value = currency.Trim().ToLower(Turkish);
        return value.StartsWith('₺') || value.Contains("türk lirası") || value is "tl" or "try";
    }

    private static bool IsAbroad(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return false;
        }

        var value = city.Trim().ToLower(Turkish);
        // "* Almanya": the 2023+ files mark every country other than Turkey with a star.
        return value.StartsWith('*') || value.Contains("yurt") || value.Contains("yurtdisi");
    }

    private static Regex Rx(string pattern) => new(pattern, RegexOptions.CultureInvariant);
}
