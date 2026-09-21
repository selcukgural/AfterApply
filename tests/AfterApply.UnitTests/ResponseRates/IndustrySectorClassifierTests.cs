using AfterApply.Application.ResponseRates;
using AfterApply.Domain.Benchmark;
using Shouldly;

namespace AfterApply.UnitTests.ResponseRates;

public class IndustrySectorClassifierTests
{
    // LinkedIn's own industry labels, as the enrichment job lifts them.
    [Theory]
    [InlineData("Software Development", BenchmarkSector.SoftwareAndIt)]
    [InlineData("IT Services and IT Consulting", BenchmarkSector.SoftwareAndIt)]
    [InlineData("Technology, Information and Internet", BenchmarkSector.SoftwareAndIt)]
    [InlineData("Banking", BenchmarkSector.FinanceAndInsurance)]
    [InlineData("Financial Services", BenchmarkSector.FinanceAndInsurance)]
    [InlineData("Insurance", BenchmarkSector.FinanceAndInsurance)]
    [InlineData("Retail", BenchmarkSector.EcommerceAndRetail)]
    [InlineData("Retail Apparel and Fashion", BenchmarkSector.EcommerceAndRetail)]
    [InlineData("Telecommunications", BenchmarkSector.Telecom)]
    [InlineData("Hospitals and Health Care", BenchmarkSector.HealthAndPharma)]
    [InlineData("Pharmaceutical Manufacturing", BenchmarkSector.HealthAndPharma)]
    [InlineData("Higher Education", BenchmarkSector.Education)]
    [InlineData("Advertising Services", BenchmarkSector.MediaAndMarketing)]
    [InlineData("Truck Transportation", BenchmarkSector.LogisticsAndTransport)]
    [InlineData("Airlines and Aviation", BenchmarkSector.LogisticsAndTransport)]
    [InlineData("Construction", BenchmarkSector.ConstructionAndRealEstate)]
    [InlineData("Real Estate", BenchmarkSector.ConstructionAndRealEstate)]
    [InlineData("Motor Vehicle Manufacturing", BenchmarkSector.ManufacturingAndIndustry)]
    [InlineData("Defense and Space Manufacturing", BenchmarkSector.ManufacturingAndIndustry)]
    [InlineData("Government Administration", BenchmarkSector.PublicAndNonProfit)]
    [InlineData("Non-profit Organizations", BenchmarkSector.PublicAndNonProfit)]
    [InlineData("Business Consulting and Services", BenchmarkSector.ConsultingAndProfessionalServices)]
    [InlineData("Staffing and Recruiting", BenchmarkSector.ConsultingAndProfessionalServices)]
    public void Classifies_LinkedIn_Industry_Labels(string industry, BenchmarkSector expected) =>
        IndustrySectorClassifier.Classify(industry).ShouldBe(expected);

    // kariyer.net's Turkish labels, with the dotted and dotless i in both cases.
    [Theory]
    [InlineData("Yazılım", BenchmarkSector.SoftwareAndIt)]
    [InlineData("Bilişim", BenchmarkSector.SoftwareAndIt)]
    [InlineData("BİLİŞİM", BenchmarkSector.SoftwareAndIt)]
    [InlineData("Bankacılık", BenchmarkSector.FinanceAndInsurance)]
    [InlineData("Sigortacılık", BenchmarkSector.FinanceAndInsurance)]
    [InlineData("Perakende", BenchmarkSector.EcommerceAndRetail)]
    [InlineData("E-ticaret", BenchmarkSector.EcommerceAndRetail)]
    [InlineData("Telekomünikasyon", BenchmarkSector.Telecom)]
    [InlineData("Sağlık", BenchmarkSector.HealthAndPharma)]
    [InlineData("İlaç", BenchmarkSector.HealthAndPharma)]
    [InlineData("Eğitim", BenchmarkSector.Education)]
    [InlineData("Reklam ve Pazarlama", BenchmarkSector.MediaAndMarketing)]
    [InlineData("Lojistik", BenchmarkSector.LogisticsAndTransport)]
    [InlineData("İNŞAAT", BenchmarkSector.ConstructionAndRealEstate)]
    [InlineData("Otomotiv", BenchmarkSector.ManufacturingAndIndustry)]
    [InlineData("Savunma Sanayii", BenchmarkSector.ManufacturingAndIndustry)]
    [InlineData("Kamu", BenchmarkSector.PublicAndNonProfit)]
    [InlineData("Danışmanlık", BenchmarkSector.ConsultingAndProfessionalServices)]
    [InlineData("İnsan Kaynakları", BenchmarkSector.ConsultingAndProfessionalServices)]
    public void Classifies_Turkish_Industry_Labels(string industry, BenchmarkSector expected) =>
        IndustrySectorClassifier.Classify(industry).ShouldBe(expected);

    [Theory]
    [InlineData("Financial Technology", BenchmarkSector.FinanceAndInsurance)]
    [InlineData("Marketing Services", BenchmarkSector.MediaAndMarketing)]
    [InlineData("Health Technology", BenchmarkSector.HealthAndPharma)]
    [InlineData("E-Learning Providers", BenchmarkSector.Education)]
    public void The_Specific_Sector_Wins_Over_The_Generic_Word(string industry, BenchmarkSector expected) =>
        IndustrySectorClassifier.Classify(industry).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Miscellaneous")]
    [InlineData("Holding")]
    public void An_Unreadable_Industry_Is_Null_Not_Other(string? industry) =>
        IndustrySectorClassifier.Classify(industry).ShouldBeNull();
}
