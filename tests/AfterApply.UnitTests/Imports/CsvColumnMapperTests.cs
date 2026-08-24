using AfterApply.Application.Imports;
using Shouldly;

namespace AfterApply.UnitTests.Imports;

public class CsvColumnMapperTests
{
    [Fact]
    public void Map_AutoDetects_English_Headers()
    {
        string[] headers = ["Company Name", "Job Title", "Applied At", "Status", "Job URL", "Location"];

        var (mapping, errors) = CsvColumnMapper.Map(headers, overrideMapping: null);

        errors.ShouldBeEmpty();
        mapping.ShouldNotBeNull();
        mapping.CompanyNameHeader.ShouldBe("Company Name");
        mapping.JobTitleHeader.ShouldBe("Job Title");
        mapping.AppliedAtHeader.ShouldBe("Applied At");
        mapping.StatusHeader.ShouldBe("Status");
        mapping.JobUrlHeader.ShouldBe("Job URL");
        mapping.LocationHeader.ShouldBe("Location");
    }

    [Fact]
    public void Map_AutoDetects_Turkish_Headers()
    {
        string[] headers = ["Şirket", "Pozisyon", "Başvuru Tarihi"];

        var (mapping, errors) = CsvColumnMapper.Map(headers, overrideMapping: null);

        errors.ShouldBeEmpty();
        mapping.ShouldNotBeNull();
        mapping.CompanyNameHeader.ShouldBe("Şirket");
        mapping.JobTitleHeader.ShouldBe("Pozisyon");
        mapping.AppliedAtHeader.ShouldBe("Başvuru Tarihi");
        mapping.StatusHeader.ShouldBeNull();
        mapping.JobUrlHeader.ShouldBeNull();
        mapping.LocationHeader.ShouldBeNull();
    }

    [Fact]
    public void Map_Missing_Required_Columns_Returns_Errors()
    {
        string[] headers = ["Notes", "Random"];

        var (mapping, errors) = CsvColumnMapper.Map(headers, overrideMapping: null);

        mapping.ShouldBeNull();
        errors.Count.ShouldBe(3);
    }

    [Fact]
    public void Map_Override_Wins_Over_Alias_Detection()
    {
        string[] headers = ["Firma Adi", "Gorev", "Tarih"];
        var overrideMapping = new Dictionary<string, string>
        {
            ["CompanyName"] = "Firma Adi",
            ["JobTitle"] = "Gorev",
            ["AppliedAt"] = "Tarih"
        };

        var (mapping, errors) = CsvColumnMapper.Map(headers, overrideMapping);

        errors.ShouldBeEmpty();
        mapping.ShouldNotBeNull();
        mapping.CompanyNameHeader.ShouldBe("Firma Adi");
        mapping.JobTitleHeader.ShouldBe("Gorev");
        mapping.AppliedAtHeader.ShouldBe("Tarih");
    }
}
