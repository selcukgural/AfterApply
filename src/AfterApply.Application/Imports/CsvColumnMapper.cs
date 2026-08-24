using AfterApply.Domain.Common;

namespace AfterApply.Application.Imports;

public sealed record ColumnMapping(
    string CompanyNameHeader,
    string JobTitleHeader,
    string AppliedAtHeader,
    string? StatusHeader,
    string? JobUrlHeader,
    string? LocationHeader);

/// <summary>
/// Maps a generic CSV's header row onto the fields an import needs. Headers are matched against
/// a TR/EN alias table (case/whitespace/Turkish-i-insensitive); an explicit <c>overrideMapping</c>
/// (field name -> header name) wins over auto-detection for a given field.
/// </summary>
public static class CsvColumnMapper
{
    private static readonly string[] CompanyNameAliases =
        ["company", "companyname", "company name", "şirket", "sirket", "firma"];

    private static readonly string[] JobTitleAliases =
        ["title", "jobtitle", "job title", "position", "pozisyon", "unvan", "ünvan"];

    private static readonly string[] AppliedAtAliases =
        ["appliedat", "applied at", "date", "applicationdate", "application date", "tarih", "başvuru tarihi", "basvuru tarihi"];

    private static readonly string[] StatusAliases = ["status", "durum"];

    private static readonly string[] JobUrlAliases =
        ["joburl", "job url", "url", "link", "ilan linki", "ilan url", "başvuru linki"];

    private static readonly string[] LocationAliases = ["location", "konum", "lokasyon", "şehir", "sehir", "city"];

    public static (ColumnMapping? Mapping, IReadOnlyList<string> Errors) Map(
        IReadOnlyList<string> headers, IReadOnlyDictionary<string, string>? overrideMapping)
    {
        var companyHeader = Resolve(headers, overrideMapping, "CompanyName", CompanyNameAliases);
        var titleHeader = Resolve(headers, overrideMapping, "JobTitle", JobTitleAliases);
        var appliedAtHeader = Resolve(headers, overrideMapping, "AppliedAt", AppliedAtAliases);
        var statusHeader = Resolve(headers, overrideMapping, "Status", StatusAliases);
        var jobUrlHeader = Resolve(headers, overrideMapping, "JobUrl", JobUrlAliases);
        var locationHeader = Resolve(headers, overrideMapping, "Location", LocationAliases);

        var errors = new List<string>();
        if (companyHeader is null)
        {
            errors.Add("CompanyName sütunu bulunamadı (beklenen başlıklar: Company, Company Name, Şirket).");
        }

        if (titleHeader is null)
        {
            errors.Add("JobTitle sütunu bulunamadı (beklenen başlıklar: Title, Position, Pozisyon).");
        }

        if (appliedAtHeader is null)
        {
            errors.Add("AppliedAt sütunu bulunamadı (beklenen başlıklar: Applied At, Date, Tarih).");
        }

        return errors.Count > 0
            ? (null, errors)
            : (new ColumnMapping(companyHeader!, titleHeader!, appliedAtHeader!, statusHeader, jobUrlHeader, locationHeader), []);
    }

    private static string? Resolve(IReadOnlyList<string> headers, IReadOnlyDictionary<string, string>? overrideMapping,
        string fieldName, string[] aliases)
    {
        if (overrideMapping is not null && overrideMapping.TryGetValue(fieldName, out var explicitHeader))
        {
            var match = headers.FirstOrDefault(h => string.Equals(h.Trim(), explicitHeader.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return headers.FirstOrDefault(h => aliases.Contains(NormalizeHeader(h)));
    }

    private static string NormalizeHeader(string header)
    {
        var folded = TurkishTextNormalizer.FoldCase(header.Trim());
        return string.Join(' ', folded.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
