using AfterApply.Application.Imports;
using AfterApply.Domain.Applications;
using Shouldly;

namespace AfterApply.UnitTests.Imports;

public class ImportRowParserTests
{
    private static readonly ColumnMapping FullMapping = new(
        CompanyNameHeader: "Company", JobTitleHeader: "Title", AppliedAtHeader: "Date",
        StatusHeader: "Status", JobUrlHeader: "Url", LocationHeader: "Location");

    private static readonly ColumnMapping MinimalMapping = new(
        CompanyNameHeader: "Company", JobTitleHeader: "Title", AppliedAtHeader: "Date",
        StatusHeader: null, JobUrlHeader: null, LocationHeader: null);

    [Fact]
    public void Parse_Valid_Row_Returns_ParsedRow()
    {
        var row = new Dictionary<string, string?>
        {
            ["Company"] = "Acme", ["Title"] = "Backend Engineer", ["Date"] = "2026-01-15",
            ["Status"] = "Interview", ["Url"] = "https://example.com/job/1", ["Location"] = "Istanbul"
        };

        var (parsed, error) = ImportRowParser.Parse(row, FullMapping);

        error.ShouldBeNull();
        parsed.ShouldNotBeNull();
        parsed.CompanyName.ShouldBe("Acme");
        parsed.JobTitle.ShouldBe("Backend Engineer");
        parsed.AppliedAt.ShouldBe(new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero));
        parsed.Status.ShouldBe(ApplicationStatus.Interview);
        parsed.JobUrl.ShouldBe("https://example.com/job/1");
        parsed.Location.ShouldBe("Istanbul");
    }

    [Fact]
    public void Parse_Defaults_Status_To_Applied_When_No_Status_Column()
    {
        var row = new Dictionary<string, string?> { ["Company"] = "Acme", ["Title"] = "Engineer", ["Date"] = "2026-01-15" };

        var (parsed, error) = ImportRowParser.Parse(row, MinimalMapping);

        error.ShouldBeNull();
        parsed.ShouldNotBeNull();
        parsed.Status.ShouldBe(ApplicationStatus.Applied);
        parsed.JobUrl.ShouldBeNull();
        parsed.Location.ShouldBeNull();
    }

    [Fact]
    public void Parse_Empty_CompanyName_Returns_Error()
    {
        var row = new Dictionary<string, string?> { ["Company"] = "  ", ["Title"] = "Engineer", ["Date"] = "2026-01-15" };

        var (parsed, error) = ImportRowParser.Parse(row, MinimalMapping);

        parsed.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_Empty_JobTitle_Returns_Error()
    {
        var row = new Dictionary<string, string?> { ["Company"] = "Acme", ["Title"] = "", ["Date"] = "2026-01-15" };

        var (parsed, error) = ImportRowParser.Parse(row, MinimalMapping);

        parsed.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_Unparseable_Date_Returns_Error()
    {
        var row = new Dictionary<string, string?> { ["Company"] = "Acme", ["Title"] = "Engineer", ["Date"] = "not-a-date" };

        var (parsed, error) = ImportRowParser.Parse(row, MinimalMapping);

        parsed.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("dd.MM.yyyy formatı", "15.01.2026")]
    [InlineData("dd/MM/yyyy formatı", "15/01/2026")]
    public void Parse_Accepts_Common_Date_Formats(string _, string rawDate)
    {
        var row = new Dictionary<string, string?> { ["Company"] = "Acme", ["Title"] = "Engineer", ["Date"] = rawDate };

        var (parsed, error) = ImportRowParser.Parse(row, MinimalMapping);

        error.ShouldBeNull();
        parsed.ShouldNotBeNull();
        parsed.AppliedAt.Year.ShouldBe(2026);
        parsed.AppliedAt.Month.ShouldBe(1);
        parsed.AppliedAt.Day.ShouldBe(15);
    }

    [Theory]
    [InlineData("Mülakat", ApplicationStatus.Interview)]
    [InlineData("mulakat", ApplicationStatus.Interview)]
    [InlineData("Reddedildi", ApplicationStatus.Rejected)]
    [InlineData("Teklif", ApplicationStatus.Offer)]
    [InlineData("Kayboldu", ApplicationStatus.Ghosted)]
    [InlineData("Rejected", ApplicationStatus.Rejected)]
    public void Parse_Recognizes_TurkishAndEnglish_Status_Aliases(string rawStatus, ApplicationStatus expected)
    {
        var row = new Dictionary<string, string?>
        {
            ["Company"] = "Acme", ["Title"] = "Engineer", ["Date"] = "2026-01-15", ["Status"] = rawStatus
        };

        var (parsed, error) = ImportRowParser.Parse(row, FullMapping);

        error.ShouldBeNull();
        parsed.ShouldNotBeNull();
        parsed.Status.ShouldBe(expected);
    }

    [Fact]
    public void Parse_Unrecognized_Status_Returns_Error()
    {
        var row = new Dictionary<string, string?>
        {
            ["Company"] = "Acme", ["Title"] = "Engineer", ["Date"] = "2026-01-15", ["Status"] = "Bilinmeyen Durum"
        };

        var (parsed, error) = ImportRowParser.Parse(row, FullMapping);

        parsed.ShouldBeNull();
        error.ShouldNotBeNull();
    }
}
