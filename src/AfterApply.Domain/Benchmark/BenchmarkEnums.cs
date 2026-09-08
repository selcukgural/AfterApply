namespace AfterApply.Domain.Benchmark;

/// <summary>
/// The field someone is applying *in* — their own target field, not the industry of any one
/// employer. A job hunt happens inside a field, so this is the axis a reader recognises
/// ("is my reply rate normal for software?").
///
/// Deliberately short. Every extra option costs completion on a form whose completion rate is
/// itself the thing being measured, and a fine-grained list splits the sample into cells too small
/// to ever cross the publication threshold.
///
/// <b>Not the same thing as <c>Company.Industry</c>, and cannot be.</b> That column holds free text
/// scraped off a LinkedIn company page ("IT Services and IT Consulting", "Yazılım Geliştirme", …),
/// 200 characters, nullable, uncontrolled. Pooling self-reported survey answers with product data
/// would need a mapping from that text onto this enum; it does not exist yet and is not needed
/// while the benchmark is survey-only.
/// </summary>
public enum BenchmarkSector
{
    SoftwareAndIt,
    FinanceAndInsurance,
    EcommerceAndRetail,
    ManufacturingAndIndustry,
    Telecom,
    HealthAndPharma,
    Education,
    ConsultingAndProfessionalServices,
    MediaAndMarketing,
    LogisticsAndTransport,
    ConstructionAndRealEstate,
    PublicAndNonProfit,
    Other
}

/// <summary>
/// How far back the reported numbers reach. Required, not optional: "127 applications, 20 replies"
/// means nothing without it, and answers covering different spans cannot be pooled into one median.
/// A fixed set rather than a date so nobody has to look anything up to answer.
/// </summary>
public enum BenchmarkPeriod
{
    LastThreeMonths,
    LastSixMonths,
    LastTwelveMonths,
    Longer
}

/// <summary>Optional. Skipping it is normal and must stay costless — it is a breakdown axis for
/// later, not something the answer depends on.</summary>
public enum BenchmarkSeniority
{
    StudentOrIntern,
    Junior,
    Mid,
    Senior,
    LeadOrAbove
}

/// <summary>Optional, and a fixed list rather than a text box: a free-text city would be
/// unpoolable ("Istanbul" / "İstanbul" / "ist") and is the one field where someone might type
/// something identifying.</summary>
public enum BenchmarkLocation
{
    Istanbul,
    Ankara,
    Izmir,
    TurkeyOther,
    Abroad,
    Remote
}
