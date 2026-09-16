using System.Globalization;
using System.Reflection;
using AfterApply.Domain.Occupations;
using CsvHelper;
using CsvHelper.Configuration;

namespace AfterApply.Infrastructure.Occupations;

/// <summary>One row of a seed CSV: what the catalogue says about an occupation in that version.</summary>
public sealed record OccupationSeedRow(string Code, string Isco08Code, OccupationSource Source, string NameTr, string NameEn);

/// <summary>
/// Reads an embedded <c>occupations.vN.csv</c>. The data migration that seeds version N calls this
/// with N, so its SQL is the same on every database it ever runs against — the CSV is versioned
/// precisely so that a later catalogue change (v2 + its own migration) cannot rewrite history.
/// </summary>
public static class OccupationSeedReader
{
    public static IReadOnlyList<OccupationSeedRow> Read(int version)
    {
        var name = $"AfterApply.Infrastructure.Occupations.Seed.occupations.v{version}.csv";
        using var stream = typeof(OccupationSeedReader).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded occupation seed '{name}' is missing.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HeaderValidated = null, MissingFieldFound = null });

        var rows = new List<OccupationSeedRow>();
        csv.Read();
        csv.ReadHeader();
        while (csv.Read())
        {
            rows.Add(new OccupationSeedRow(
                csv.GetField("code")!.Trim(),
                csv.GetField("isco08Code")!.Trim(),
                Enum.Parse<OccupationSource>(csv.GetField("source")!.Trim()),
                csv.GetField("nameTr")!.Trim(),
                csv.GetField("nameEn")!.Trim()));
        }

        return rows;
    }

    /// <summary>The upsert statement a seed migration runs: insert every row of the version, or
    /// bring an existing row (matched by code) up to date and back to active. Emitted as one
    /// statement so <c>migrations script</c> shows a single, readable INSERT.</summary>
    public static string BuildUpsertSql(int version)
    {
        var rows = Read(version);
        var values = rows.Select(r =>
        {
            var occupation = Occupation.Create(r.Code, r.Isco08Code, r.NameTr, r.NameEn, r.Source);
            return "(" + string.Join(", ",
                Literal(occupation.Id.ToString()), Literal(occupation.Code), Literal(occupation.Isco08Code),
                Literal(occupation.NameTr), Literal(occupation.NameEn),
                Literal(occupation.NormalizedNameTr), Literal(occupation.NormalizedNameEn),
                Literal(occupation.Source.ToString()), "TRUE") + ")";
        });

        return "INSERT INTO \"Occupations\" (\"Id\", \"Code\", \"Isco08Code\", \"NameTr\", \"NameEn\", \"NormalizedNameTr\", \"NormalizedNameEn\", \"Source\", \"IsActive\")\n"
               + "VALUES\n" + string.Join(",\n", values) + "\n"
               + "ON CONFLICT (\"Code\") DO UPDATE SET\n"
               + "  \"Isco08Code\" = EXCLUDED.\"Isco08Code\", \"NameTr\" = EXCLUDED.\"NameTr\", \"NameEn\" = EXCLUDED.\"NameEn\",\n"
               + "  \"NormalizedNameTr\" = EXCLUDED.\"NormalizedNameTr\", \"NormalizedNameEn\" = EXCLUDED.\"NormalizedNameEn\",\n"
               + "  \"Source\" = EXCLUDED.\"Source\", \"IsActive\" = TRUE;";
    }

    private static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
}
