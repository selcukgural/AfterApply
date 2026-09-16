using System.Text.RegularExpressions;
using AfterApply.Domain.Occupations;
using AfterApply.Infrastructure.Occupations;
using Shouldly;

namespace AfterApply.UnitTests.Occupations;

/// <summary>The catalogue CSV is the review surface for the occupation list; this pins the
/// invariants the seed migration and the search rely on.</summary>
public class OccupationSeedTests
{
    private static readonly IReadOnlyList<OccupationSeedRow> Rows = OccupationSeedReader.Read(1);

    [Fact]
    public void Has_Every_Isco08_Unit_Group_And_A_Broad_Curated_Layer()
    {
        Rows.Count(r => r.Source == OccupationSource.Isco08).ShouldBe(436);
        Rows.Count(r => r.Source == OccupationSource.Curated).ShouldBeGreaterThanOrEqualTo(150);
    }

    [Fact]
    public void Codes_Are_Unique_And_Well_Formed()
    {
        Rows.Select(r => r.Code).ShouldBeUnique();
        foreach (var row in Rows)
        {
            row.Code.Length.ShouldBeLessThanOrEqualTo(Occupation.MaxCodeLength);
            Regex.IsMatch(row.Isco08Code, "^[0-9]{4}$").ShouldBeTrue($"{row.Code}: ISCO code '{row.Isco08Code}'");
            if (row.Source == OccupationSource.Isco08)
            {
                row.Code.ShouldBe(row.Isco08Code);
            }
            else
            {
                Regex.IsMatch(row.Code, "^EK-[0-9]{4}$").ShouldBeTrue(row.Code);
            }
        }
    }

    [Fact]
    public void Curated_Rows_Map_To_An_Existing_Unit_Group()
    {
        var unitGroups = Rows.Where(r => r.Source == OccupationSource.Isco08).Select(r => r.Code).ToHashSet();
        foreach (var row in Rows.Where(r => r.Source == OccupationSource.Curated))
        {
            unitGroups.ShouldContain(row.Isco08Code, $"{row.Code} {row.NameEn}");
        }
    }

    [Fact]
    public void Every_Row_Has_Both_Names_Within_The_Column_Width()
    {
        foreach (var row in Rows)
        {
            row.NameTr.ShouldNotBeNullOrWhiteSpace(row.Code);
            row.NameEn.ShouldNotBeNullOrWhiteSpace(row.Code);
            row.NameTr.Length.ShouldBeLessThanOrEqualTo(Occupation.MaxNameLength, row.Code);
            row.NameEn.Length.ShouldBeLessThanOrEqualTo(Occupation.MaxNameLength, row.Code);
        }
    }

    [Fact]
    public void No_Two_Rows_Share_A_Name_In_Either_Language()
    {
        // Two rows that fold to the same search string would be indistinguishable in the typeahead.
        Rows.Select(r => OccupationName.Normalize(r.NameTr)).ShouldBeUnique();
        Rows.Select(r => OccupationName.Normalize(r.NameEn)).ShouldBeUnique();
    }

    [Fact]
    public void The_Upsert_Statement_Covers_Every_Row_Once()
    {
        var sql = OccupationSeedReader.BuildUpsertSql(1);

        sql.ShouldStartWith("INSERT INTO \"Occupations\"");
        sql.ShouldContain("ON CONFLICT (\"Code\") DO UPDATE");
        Regex.Matches(sql, @"\n\('").Count.ShouldBe(Rows.Count);
        // Quotes in names are escaped, never left to break the statement.
        sql.ShouldNotContain("Ships' ");
        sql.ShouldContain("Ships'' ");
    }
}
