using System.Globalization;
using AfterApply.Application.SalaryMarket;
using CsvHelper;
using CsvHelper.Configuration;

namespace AfterApply.Infrastructure.SalaryMarket;

/// <summary>
/// The published survey figures, as two CSV files compiled into this assembly: which surveys fed
/// each year (<see cref="EditionsFile"/>) and the figures themselves (<see cref="CellsFile"/>).
/// Only aggregates are in the repository — the answers they were computed from stay on the
/// machine that ran <c>tools/SalarySurveyImport</c>. A new survey year is a new pair of files from
/// that tool and a deploy; git history is the record of what was published when.
/// </summary>
public static class SalaryMarketSeed
{
    public const string EditionsFile = "salary-market-editions.csv";
    public const string CellsFile = "salary-market-cells.csv";

    private static readonly CsvConfiguration Config = new(CultureInfo.InvariantCulture)
    {
        HeaderValidated = null,
        MissingFieldFound = null,
        // LF, like every other text file in the repository, so a regenerated seed diffs cleanly.
        NewLine = "\n"
    };

    public static IReadOnlyList<SalarySurveyEdition> ReadEditions() => ReadEditions(Open(EditionsFile));

    public static IReadOnlyList<SalaryMarketCell> ReadCells() => ReadCells(Open(CellsFile));

    public static IReadOnlyList<SalarySurveyEdition> ReadEditions(TextReader text)
    {
        using var csv = new CsvReader(text, Config);
        var editions = new List<SalarySurveyEdition>();
        csv.Read();
        csv.ReadHeader();
        while (csv.Read())
        {
            editions.Add(new SalarySurveyEdition(
                Int(csv, "year"), Text(csv, "sourceCode"), Text(csv, "sourceName"), Text(csv, "sourceUrl"),
                Text(csv, "publishedMonth"), Int(csv, "responses"), Int(csv, "used")));
        }

        return editions;
    }

    public static IReadOnlyList<SalaryMarketCell> ReadCells(TextReader text)
    {
        using var csv = new CsvReader(text, Config);
        var cells = new List<SalaryMarketCell>();
        csv.Read();
        csv.ReadHeader();
        while (csv.Read())
        {
            var atLeast = Text(csv, "atLeast");
            cells.Add(new SalaryMarketCell(
                Int(csv, "year"), Text(csv, "group"), Enum.Parse<SalaryMarketDimension>(Text(csv, "dimension")), Text(csv, "bucket"),
                new SalaryStats(Int(csv, "count"), Int(csv, "p10"), Int(csv, "p25"), Int(csv, "p50"), Int(csv, "p75"), Int(csv, "p90"),
                    atLeast.Length == 0 ? SalaryPercentiles.None : Enum.Parse<SalaryPercentiles>(atLeast.Replace('|', ',')))));
        }

        return cells;
    }

    public static void WriteEditions(TextWriter text, IEnumerable<SalarySurveyEdition> editions)
    {
        using var csv = new CsvWriter(text, Config, leaveOpen: true);
        foreach (var header in new[] { "year", "sourceCode", "sourceName", "sourceUrl", "publishedMonth", "responses", "used" })
        {
            csv.WriteField(header);
        }

        csv.NextRecord();
        foreach (var e in editions.OrderBy(e => e.Year).ThenBy(e => e.SourceCode, StringComparer.Ordinal))
        {
            csv.WriteField(e.Year);
            csv.WriteField(e.SourceCode);
            csv.WriteField(e.SourceName);
            csv.WriteField(e.SourceUrl);
            csv.WriteField(e.PublishedMonth);
            csv.WriteField(e.Responses);
            csv.WriteField(e.Used);
            csv.NextRecord();
        }
    }

    public static void WriteCells(TextWriter text, IEnumerable<SalaryMarketCell> cells)
    {
        using var csv = new CsvWriter(text, Config, leaveOpen: true);
        foreach (var header in new[] { "year", "group", "dimension", "bucket", "count", "p10", "p25", "p50", "p75", "p90", "atLeast" })
        {
            csv.WriteField(header);
        }

        csv.NextRecord();
        foreach (var c in cells)
        {
            csv.WriteField(c.Year);
            csv.WriteField(c.Group);
            csv.WriteField(c.Dimension.ToString());
            csv.WriteField(c.Bucket);
            csv.WriteField(c.Stats.Count);
            csv.WriteField(c.Stats.P10);
            csv.WriteField(c.Stats.P25);
            csv.WriteField(c.Stats.P50);
            csv.WriteField(c.Stats.P75);
            csv.WriteField(c.Stats.P90);
            // "P75|P90": a pipe, so the field needs no quoting and reads at a glance in a diff.
            csv.WriteField(c.Stats.AtLeast == SalaryPercentiles.None ? string.Empty : c.Stats.AtLeast.ToString().Replace(", ", "|"));
            csv.NextRecord();
        }
    }

    private static StreamReader Open(string file)
    {
        var name = $"AfterApply.Infrastructure.SalaryMarket.Seed.{file}";
        var stream = typeof(SalaryMarketSeed).Assembly.GetManifestResourceStream(name)
                     ?? throw new InvalidOperationException($"Embedded salary seed '{name}' is missing.");
        return new StreamReader(stream, System.Text.Encoding.UTF8);
    }

    private static string Text(CsvReader csv, string field) => csv.GetField(field)?.Trim() ?? string.Empty;

    private static int Int(CsvReader csv, string field) => int.Parse(Text(csv, field), NumberStyles.Integer, CultureInfo.InvariantCulture);
}
