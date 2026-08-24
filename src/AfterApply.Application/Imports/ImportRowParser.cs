using System.Globalization;
using AfterApply.Domain.Applications;

namespace AfterApply.Application.Imports;

public sealed record ParsedImportRow(
    string CompanyName,
    string JobTitle,
    DateTimeOffset AppliedAt,
    ApplicationStatus Status,
    string? JobUrl,
    string? Location);

/// <summary>
/// Validates and parses one raw CSV row (already located via <see cref="ColumnMapping"/>) into a
/// <see cref="ParsedImportRow"/>, or returns a human-readable error. Pure function — no I/O.
/// </summary>
public static class ImportRowParser
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "yyyy/MM/dd", "dd.MM.yyyy", "dd/MM/yyyy", "MM/dd/yyyy",
        "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss"
    ];

    private static readonly Dictionary<string, ApplicationStatus> StatusAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["applied"] = ApplicationStatus.Applied,
        ["başvuruldu"] = ApplicationStatus.Applied,
        ["basvuruldu"] = ApplicationStatus.Applied,
        ["screening"] = ApplicationStatus.Screening,
        ["ön eleme"] = ApplicationStatus.Screening,
        ["on eleme"] = ApplicationStatus.Screening,
        ["interview"] = ApplicationStatus.Interview,
        ["mülakat"] = ApplicationStatus.Interview,
        ["mulakat"] = ApplicationStatus.Interview,
        ["technical interview"] = ApplicationStatus.TechnicalInterview,
        ["teknik mülakat"] = ApplicationStatus.TechnicalInterview,
        ["teknik mulakat"] = ApplicationStatus.TechnicalInterview,
        ["final interview"] = ApplicationStatus.FinalInterview,
        ["final mülakat"] = ApplicationStatus.FinalInterview,
        ["offer"] = ApplicationStatus.Offer,
        ["teklif"] = ApplicationStatus.Offer,
        ["accepted"] = ApplicationStatus.Accepted,
        ["kabul edildi"] = ApplicationStatus.Accepted,
        ["rejected"] = ApplicationStatus.Rejected,
        ["reddedildi"] = ApplicationStatus.Rejected,
        ["withdrawn"] = ApplicationStatus.Withdrawn,
        ["geri çekildi"] = ApplicationStatus.Withdrawn,
        ["geri cekildi"] = ApplicationStatus.Withdrawn,
        ["ghosted"] = ApplicationStatus.Ghosted,
        ["kayboldu"] = ApplicationStatus.Ghosted
    };

    public static (ParsedImportRow? Row, string? Error) Parse(IReadOnlyDictionary<string, string?> rawRow, ColumnMapping mapping)
    {
        var companyName = GetValue(rawRow, mapping.CompanyNameHeader);
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return (null, "CompanyName boş olamaz.");
        }

        var jobTitle = GetValue(rawRow, mapping.JobTitleHeader);
        if (string.IsNullOrWhiteSpace(jobTitle))
        {
            return (null, "JobTitle boş olamaz.");
        }

        var appliedAtRaw = GetValue(rawRow, mapping.AppliedAtHeader);
        if (!TryParseDate(appliedAtRaw, out var appliedAt))
        {
            return (null, $"AppliedAt tarihi ayrıştırılamadı: '{appliedAtRaw}'.");
        }

        var status = ApplicationStatus.Applied;
        if (mapping.StatusHeader is not null)
        {
            var statusRaw = GetValue(rawRow, mapping.StatusHeader);
            if (!string.IsNullOrWhiteSpace(statusRaw) && !TryParseStatus(statusRaw, out status))
            {
                return (null, $"Status tanınamadı: '{statusRaw}'.");
            }
        }

        var jobUrl = mapping.JobUrlHeader is not null ? NullIfEmpty(GetValue(rawRow, mapping.JobUrlHeader)) : null;
        var location = mapping.LocationHeader is not null ? NullIfEmpty(GetValue(rawRow, mapping.LocationHeader)) : null;

        return (new ParsedImportRow(companyName.Trim(), jobTitle.Trim(), appliedAt, status, jobUrl, location), null);
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> row, string header) =>
        row.GetValueOrDefault(header);

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseDate(string? raw, out DateTimeOffset appliedAt)
    {
        appliedAt = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out appliedAt))
        {
            return true;
        }

        foreach (var format in DateFormats)
        {
            if (DateTimeOffset.TryParseExact(trimmed, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out appliedAt))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseStatus(string raw, out ApplicationStatus status)
    {
        var trimmed = raw.Trim();
        return StatusAliases.TryGetValue(trimmed, out status) || Enum.TryParse(trimmed, ignoreCase: true, out status);
    }
}
