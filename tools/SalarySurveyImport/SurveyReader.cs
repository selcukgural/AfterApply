using System.Text.Json;
using AfterApply.Application.SalaryMarket.Import;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace SalarySurveyImport;

/// <summary>Reads one edition's file into <see cref="SurveyResponse"/>s — only the six fields
/// the pages use; everything else in the file (gender, technologies, company type) is never read.</summary>
internal static class SurveyReader
{
    public static async Task<IReadOnlyList<SurveyResponse>> ReadAsync(SurveyEdition edition, string path, CancellationToken cancellationToken) =>
        edition.Layout switch
        {
            FileLayout.Json => await ReadJsonAsync(edition, path, cancellationToken),
            FileLayout.Xlsx2018 => ReadXlsx(path, hasHeader: false)
                .Select(row => new SurveyResponse(edition.Year, edition.SourceCode,
                    Position: At(row, 0), Level: At(row, 1), Experience: At(row, 2), City: At(row, 4), Currency: null, Salary: At(row, 5)))
                .ToList(),
            FileLayout.XlsxWithHeader => ReadXlsxWithHeader(edition, path),
            _ => throw new ArgumentOutOfRangeException(nameof(edition))
        };

    private static async Task<IReadOnlyList<SurveyResponse>> ReadJsonAsync(SurveyEdition edition, string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var array = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement
            : document.RootElement.EnumerateObject().First(p => p.Value.ValueKind == JsonValueKind.Array).Value;

        var responses = new List<SurveyResponse>();
        foreach (var item in array.EnumerateArray())
        {
            responses.Add(new SurveyResponse(edition.Year, edition.SourceCode,
                Position: Field(item, edition.PositionKey),
                Level: Field(item, edition.LevelKey),
                Experience: Field(item, edition.ExperienceKey),
                City: Field(item, edition.CityKey),
                Currency: edition.CurrencyKey is null ? null : Field(item, edition.CurrencyKey) ?? string.Empty,
                Salary: Field(item, edition.SalaryKey)));
        }

        return responses;
    }

    /// <summary>The value under <paramref name="key"/>, or under the one key that starts with it
    /// (the 2026 salary question is a sentence).</summary>
    private static string? Field(JsonElement item, string key)
    {
        if (!item.TryGetProperty(key, out var value))
        {
            var match = item.EnumerateObject().FirstOrDefault(p => p.Name.StartsWith(key, StringComparison.Ordinal));
            if (match.Name is null)
            {
                return null;
            }

            value = match.Value;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static List<SurveyResponse> ReadXlsxWithHeader(SurveyEdition edition, string path)
    {
        var rows = ReadXlsx(path, hasHeader: false);
        var header = rows[0].Select(h => h?.Trim() ?? string.Empty).ToList();
        int Column(string name) => header.IndexOf(name) is var i and >= 0 ? i : throw new InvalidDataException($"{path}: no '{name}' column.");

        var (position, level, experience, city, salary) =
            (Column(edition.PositionKey), Column(edition.LevelKey), Column(edition.ExperienceKey), Column(edition.CityKey), Column(edition.SalaryKey));
        return rows.Skip(1)
            .Select(row => new SurveyResponse(edition.Year, edition.SourceCode,
                At(row, position), At(row, level), At(row, experience), At(row, city), Currency: null, At(row, salary)))
            .ToList();
    }

    /// <summary>The first sheet as rows of cell text, gaps kept in place by column letter.</summary>
    private static List<string?[]> ReadXlsx(string path, bool hasHeader)
    {
        using var document = SpreadsheetDocument.Open(path, false);
        var workbook = document.WorkbookPart ?? throw new InvalidDataException($"{path}: not a workbook.");
        var shared = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(s => s.InnerText).ToList() ?? [];
        var sheetId = workbook.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault()?.Id?.Value
                      ?? throw new InvalidDataException($"{path}: no sheet.");
        var sheet = ((WorksheetPart)workbook.GetPartById(sheetId)).Worksheet
                    ?? throw new InvalidDataException($"{path}: empty sheet.");

        var rows = new List<string?[]>();
        foreach (var row in sheet.Descendants<Row>())
        {
            var cells = row.Elements<Cell>().ToList();
            var width = cells.Count == 0 ? 0 : cells.Max(c => ColumnIndex(c.CellReference?.Value)) + 1;
            var values = new string?[width];
            foreach (var cell in cells)
            {
                var text = cell.CellValue?.Text ?? cell.InlineString?.InnerText;
                if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(text, out var index))
                {
                    text = shared[index];
                }

                values[ColumnIndex(cell.CellReference?.Value)] = text;
            }

            if (values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                rows.Add(values);
            }
        }

        return hasHeader ? rows.Skip(1).ToList() : rows;
    }

    private static int ColumnIndex(string? reference)
    {
        var index = 0;
        foreach (var ch in reference ?? "A")
        {
            if (!char.IsLetter(ch))
            {
                break;
            }

            index = index * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return index - 1;
    }

    private static string? At(string?[] row, int index) => index < row.Length ? row[index] : null;
}
