using AfterApply.Application.SalaryMarket;
using AfterApply.Application.SalaryMarket.Contracts;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.SalaryMarket;

/// <summary>
/// Serves the compiled-in survey figures. Built once per process (a singleton): the seed is a
/// few hundred kilobytes of CSV inside the assembly, and every response is derived from it in
/// memory, so there is nothing to cache and nothing to wait on.
/// </summary>
public sealed class SalaryMarketService : ISalaryMarketService
{
    private readonly int _minimum;
    private readonly IReadOnlyList<SalarySurveyEdition> _editions;
    private readonly ILookup<string, SalaryMarketCell> _cellsByGroup;
    private readonly SalaryOccupationsResponse _occupations;

    public SalaryMarketService(IOptions<SalaryMarketOptions> options)
        : this(options, SalaryMarketSeed.ReadEditions(), SalaryMarketSeed.ReadCells())
    {
    }

    /// <summary>From given seed rows rather than the compiled-in ones — for tests.</summary>
    public SalaryMarketService(IOptions<SalaryMarketOptions> options, IReadOnlyList<SalarySurveyEdition> editions,
        IReadOnlyList<SalaryMarketCell> cells)
    {
        _minimum = options.Value.MinimumResponses;
        _editions = editions;
        // The threshold, applied where every read path starts: a cell under it does not exist for
        // the list, the occupation page or anything added later.
        _cellsByGroup = cells.Where(IsPublishable).ToLookup(c => c.Group, StringComparer.Ordinal);
        _occupations = BuildOccupations();
    }

    public SalaryOccupationsResponse GetOccupations() => _occupations;

    public SalaryOccupationResponse? GetOccupation(string slug)
    {
        var group = SalaryMarketGroups.BySlug(slug);
        if (group is null)
        {
            return null;
        }

        var cells = _cellsByGroup[group.Slug].ToList();
        var years = cells
            .Where(c => c.Dimension == SalaryMarketDimension.All)
            .OrderBy(c => c.Year)
            .Select(overall => new SalaryOccupationYearResponse(
                overall.Year,
                SalaryStatsResponse.From(overall.Stats),
                Enum.GetValues<SalaryMarketLevel>()
                    .Select(level => cells.FirstOrDefault(c => c.Year == overall.Year && c.Dimension == SalaryMarketDimension.Level && c.Bucket == level.ToString()))
                    .OfType<SalaryMarketCell>()
                    .Select(c => new SalaryLevelStatsResponse(c.Bucket, SalaryStatsResponse.From(c.Stats)))
                    .ToList(),
                Enum.GetValues<SalaryMarketExperience>()
                    .Select(range => cells.FirstOrDefault(c => c.Year == overall.Year && c.Dimension == SalaryMarketDimension.Experience && c.Bucket == range.ToString()))
                    .OfType<SalaryMarketCell>()
                    .Select(c => new SalaryExperienceStatsResponse(c.Bucket, SalaryStatsResponse.From(c.Stats)))
                    .ToList()))
            .ToList();

        if (years.Count == 0)
        {
            return null;
        }

        var shownYears = years.Select(y => y.Year).ToHashSet();
        return new SalaryOccupationResponse(group.Slug, group.NameTr, group.NameEn, _minimum, years,
            Editions(_editions.Where(e => shownYears.Contains(e.Year))));
    }

    private SalaryOccupationsResponse BuildOccupations()
    {
        var rows = new List<SalaryOccupationSummaryResponse>();
        foreach (var group in SalaryMarketGroups.All)
        {
            var overall = _cellsByGroup[group.Slug]
                .Where(c => c.Dimension == SalaryMarketDimension.All)
                .OrderBy(c => c.Year)
                .ToList();
            if (overall.Count == 0)
            {
                continue;
            }

            var latest = overall[^1];
            var previous = overall.FirstOrDefault(c => c.Year == latest.Year - 1);
            rows.Add(new SalaryOccupationSummaryResponse(group.Slug, group.NameTr, group.NameEn, latest.Year,
                SalaryStatsResponse.From(latest.Stats), previous?.Stats.P50,
                overall.Select(c => new SalaryTrendPointResponse(c.Year, c.Stats.Count, c.Stats.P50)).ToList()));
        }

        return new SalaryOccupationsResponse(_minimum, Editions(_editions), rows);
    }

    private bool IsPublishable(SalaryMarketCell cell) =>
        cell.Stats.Count >= _minimum && SalaryMarketGroups.BySlug(cell.Group) is not null;

    private static List<SalarySurveyEditionResponse> Editions(IEnumerable<SalarySurveyEdition> editions) =>
        editions
            .OrderBy(e => e.Year)
            .ThenBy(e => e.SourceCode, StringComparer.Ordinal)
            .Select(e => new SalarySurveyEditionResponse(e.Year, e.SourceCode, e.SourceName, e.SourceUrl, e.PublishedMonth, e.Responses, e.Used))
            .ToList();
}
