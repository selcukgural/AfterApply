using System.Text.RegularExpressions;
using FluentValidation;

namespace AfterApply.Application.JobSearch.Validators;

/// <summary>
/// Rules shared by the job-search validators. They are deliberately strict about shape: the
/// provider answers a malformed parameter with a 400 that still costs a round trip (and possibly
/// a credit), so anything it would reject is refused here first.
/// </summary>
internal static partial class JobSearchValidationRules
{
    public const int MaxQueryLength = 200;
    public const int MaxLocationLength = 200;
    public const int MaxTitleLength = 120;
    public const int MaxCursorLength = 2000;
    public const int MaxListItems = 4;
    public const int MaxPublisherItems = 10;
    public const int MaxPublisherLength = 60;
    // Provider job ids are ~400-character tokens (see JobSearchJob.MaxJobIdLength).
    public const int MaxJobIdLength = AfterApply.Domain.JobSearch.JobSearchJob.MaxJobIdLength;
    public const int UpstreamMaxPages = 20;
    public const int UpstreamMaxJobIds = 20;
    public const int MaxRadiusKm = 500;

    [GeneratedRegex("^[A-Za-z]{2}$", RegexOptions.CultureInvariant)]
    public static partial Regex CountryCode();

    [GeneratedRegex("^[A-Za-z]{2,3}$", RegexOptions.CultureInvariant)]
    public static partial Regex LanguageCode();

    public static IRuleBuilderOptions<T, string?> MustBeCountryCode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => v is null || CountryCode().IsMatch(v)).WithMessage("Must be a two-letter ISO 3166-1 country code.");

    public static IRuleBuilderOptions<T, string?> MustBeLanguageCode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => v is null || LanguageCode().IsMatch(v)).WithMessage("Must be a two- or three-letter ISO 639 language code.");

    public static IRuleBuilderOptions<T, string?> MustBeEnumList<T, TEnum>(this IRuleBuilder<T, string?> rule)
        where TEnum : struct, Enum =>
        rule.Must(v => JobSearchCsv.IsValidEnumList<TEnum>(v))
            .WithMessage($"Must be a comma-separated list of: {string.Join(", ", Enum.GetNames<TEnum>())}.")
            .Must(v => JobSearchCsv.Split(v).Count <= MaxListItems)
            .WithMessage($"At most {MaxListItems} values.");
}
