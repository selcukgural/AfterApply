using System.Globalization;
using System.Text.Json;
using AfterApply.Application.JobSearch.Contracts;

namespace AfterApply.Infrastructure.JobSearch;

/// <summary>
/// Provider shapes → product DTOs, every field. Pure, and tolerant: the provider sends null for
/// most of a posting most of the time, so nulls become nulls and missing lists become empty ones,
/// never an exception. The only fields that change shape on the way are the five salary fields
/// (grouped) and the two "posted at" fields (kept, plus a parsed <c>PostedAtUtc</c>).
/// </summary>
public static class JobSearchMapper
{
    public static JobSearchJobSummaryResponse ToSummary(JSearchJob job) =>
        new(
            job.JobId ?? string.Empty,
            job.JobTitle,
            job.EmployerName,
            job.EmployerLogo,
            job.EmployerWebsite,
            job.JobPublisher,
            job.JobEmploymentType,
            List(job.JobEmploymentTypes),
            job.JobApplyLink,
            job.JobApplyIsDirect,
            ApplyOptions(job.ApplyOptions),
            job.JobDescription,
            job.JobIsRemote,
            job.JobPostedAt,
            job.JobPostedAtTimestamp,
            PostedAt(job.JobPostedAtTimestamp, job.JobPostedAtDatetimeUtc),
            job.JobLocation,
            job.JobCity,
            job.JobState,
            job.JobCountry,
            job.JobLatitude,
            job.JobLongitude,
            List(job.JobBenefits),
            List(job.JobBenefitsStrings),
            job.JobGoogleLink,
            Salary(job),
            job.JobOnetSoc,
            job.JobOnetJobZone);

    public static JobSearchJobDetailResponse ToDetail(JSearchJob job) =>
        new(
            job.JobId ?? string.Empty,
            job.JobTitle,
            job.EmployerName,
            job.EmployerLogo,
            job.EmployerWebsite,
            job.JobPublisher,
            job.JobEmploymentType,
            List(job.JobEmploymentTypes),
            job.JobApplyLink,
            job.JobApplyIsDirect,
            ApplyOptions(job.ApplyOptions),
            job.JobDescription,
            job.JobIsRemote,
            job.JobPostedAt,
            job.JobPostedAtTimestamp,
            PostedAt(job.JobPostedAtTimestamp, job.JobPostedAtDatetimeUtc),
            job.JobLocation,
            job.JobCity,
            job.JobState,
            job.JobCountry,
            job.JobLatitude,
            job.JobLongitude,
            List(job.JobBenefits),
            List(job.JobBenefitsStrings),
            job.JobGoogleLink,
            Salary(job),
            job.JobOnetSoc,
            job.JobOnetJobZone,
            Highlights(job.JobHighlights),
            EmployerReviews(job.EmployerReviews),
            job.WorkArrangement,
            job.SeniorityLevel,
            job.RequiredExperienceYears is { } years ? (int?)Math.Round(years) : null,
            job.EducationRequired is { } education ? new JobSearchEducationResponse(education.Level, education.Field) : null,
            job.VisaSponsorship,
            job.RelocationRequired,
            job.RelocationAssistance,
            job.ContractDuration,
            job.StartDate,
            List(job.RequiredTechnologies),
            List(job.PreferredTechnologies),
            List(job.Methodologies),
            job.Industry,
            job.JobFunction,
            job.HasManagementResponsibilities,
            job.AiMlInvolved,
            List(job.BenefitsExtended),
            List(job.SoftSkills));

    public static JobSearchSalaryEstimateResponse ToSalaryEstimate(JSearchSalaryEstimate estimate) =>
        new(
            estimate.Location,
            estimate.JobTitle,
            estimate.MinSalary,
            estimate.MaxSalary,
            estimate.MedianSalary,
            estimate.MinBaseSalary,
            estimate.MaxBaseSalary,
            estimate.MedianBaseSalary,
            estimate.MinAdditionalPay,
            estimate.MaxAdditionalPay,
            estimate.MedianAdditionalPay,
            estimate.SalaryPeriod,
            estimate.SalaryCurrency,
            ToInt(estimate.SalaryCount),
            ParseUtc(estimate.SalariesUpdatedAt),
            estimate.PublisherName,
            estimate.PublisherLink,
            estimate.Confidence);

    public static JobSearchCompanySalaryResponse ToCompanySalary(JSearchCompanySalary salary) =>
        new(
            salary.Company,
            salary.Location,
            salary.JobTitle,
            salary.MinSalary,
            salary.MaxSalary,
            salary.MedianSalary,
            salary.MinBaseSalary,
            salary.MaxBaseSalary,
            salary.MedianBaseSalary,
            salary.MinAdditionalPay,
            salary.MaxAdditionalPay,
            salary.MedianAdditionalPay,
            salary.SalaryPeriod,
            salary.SalaryCurrency,
            ToInt(salary.SalaryCount),
            salary.Confidence);

    private static IReadOnlyList<string> List(IReadOnlyList<string>? values) =>
        values is null ? [] : values.Where(v => v is not null).ToArray();

    private static IReadOnlyList<JobSearchApplyOptionResponse> ApplyOptions(IReadOnlyList<JSearchApplyOption>? options) =>
        options is null
            ? []
            : options.Where(o => o is not null)
                .Select(o => new JobSearchApplyOptionResponse(o.Publisher, o.ApplyLink, o.IsDirect))
                .ToArray();

    private static JobSearchSalaryResponse? Salary(JSearchJob job) =>
        job.JobSalary is null && job.JobSalaryString is null && job.JobMinSalary is null
        && job.JobMaxSalary is null && job.JobSalaryPeriod is null
            ? null
            : new JobSearchSalaryResponse(job.JobSalary, job.JobSalaryString, job.JobMinSalary, job.JobMaxSalary, job.JobSalaryPeriod);

    /// <summary>The unix timestamp when present, else the ISO string; null when neither parses.</summary>
    private static DateTimeOffset? PostedAt(long? timestamp, string? iso)
    {
        if (timestamp is { } seconds && seconds > 0)
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Fall through to the string.
            }
        }

        return ParseUtc(iso);
    }

    private static DateTimeOffset? ParseUtc(string? iso) =>
        DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    private static int? ToInt(long? value) => value is { } v && v is >= int.MinValue and <= int.MaxValue ? (int)v : null;

    /// <summary>The search endpoint sends <c>{}</c>; details sends an object with capitalised
    /// keys. Anything else (an array, null) is "no highlights".</summary>
    private static JobSearchHighlightsResponse? Highlights(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Object } highlights)
        {
            return null;
        }

        var qualifications = StringArray(highlights, "Qualifications");
        var benefits = StringArray(highlights, "Benefits");
        var responsibilities = StringArray(highlights, "Responsibilities");
        return qualifications.Count == 0 && benefits.Count == 0 && responsibilities.Count == 0
            ? null
            : new JobSearchHighlightsResponse(qualifications, benefits, responsibilities);
    }

    private static IReadOnlyList<JobSearchEmployerReviewResponse> EmployerReviews(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } reviews)
        {
            return [];
        }

        var result = new List<JobSearchEmployerReviewResponse>();
        foreach (var item in reviews.EnumerateArray())
        {
            JSearchEmployerReview? review;
            try
            {
                review = item.Deserialize<JSearchEmployerReview>(JSearchWireValues.JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (review is not null)
            {
                result.Add(new JobSearchEmployerReviewResponse(review.Publisher, review.EmployerName, review.Score,
                    review.NumStars, ToInt(review.ReviewCount), review.MaxScore, review.ReviewsLink));
            }
        }

        return result;
    }

    private static IReadOnlyList<string> StringArray(JsonElement parent, string name)
    {
        foreach (var property in parent.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                || property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return property.Value.EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString()!)
                .ToArray();
        }

        return [];
    }
}
