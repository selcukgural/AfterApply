using AfterApply.Domain.Benchmark;

namespace AfterApply.Application.ResponseRates;

/// <summary>
/// Maps a company's free-text industry — the label the enrichment job lifts off its LinkedIn or
/// kariyer.net profile ("Software Development", "Bankacılık", "IT Services and IT Consulting")
/// — onto the benchmark's thirteen sectors, so the public response-rates table and the
/// account-free benchmark speak the same list and a reader can put the two side by side.
///
/// Keyword rules, first match wins, ordered so the specific beats the generic: "fintech" and
/// "financial technology" land in finance before "technology" can claim them for software,
/// "defense and space manufacturing" is manufacturing before "government" could make it public,
/// media sits before retail so "marketing" is not read as "market", and software comes before
/// consulting because LinkedIn's own label is "IT Services and IT Consulting". Anything unmatched
/// is null, not <see cref="BenchmarkSector.Other"/>: "Other" is a sector a person can pick on the
/// benchmark form, whereas an industry we could not read is simply not in the table.
///
/// Turkish dotted/dotless i is folded by hand before the case-insensitive match: neither
/// invariant nor tr-TR lower-casing gets both "İnşaat" and "IT Services" right.
/// </summary>
public static class IndustrySectorClassifier
{
    private static readonly (BenchmarkSector Sector, string[] Keywords)[] Rules =
    [
        (BenchmarkSector.FinanceAndInsurance,
        [
            "bank", "financ", "insurance", "fintech", "investment", "capital markets", "venture", "payment",
            "banka", "finans", "sigorta", "yatırım", "ödeme", "leasing", "faktoring"
        ]),
        (BenchmarkSector.HealthAndPharma,
        [
            "health", "hospital", "pharma", "medical", "biotech", "clinic", "dental", "veterinar",
            "sağlık", "hastane", "ilaç", "medikal", "klinik", "eczac"
        ]),
        (BenchmarkSector.Education,
        [
            "education", "e-learning", "university", "school", "academ", "training",
            "eğitim", "üniversite", "okul", "akademi"
        ]),
        (BenchmarkSector.Telecom,
        [
            "telecom", "wireless carrier", "telekom", "haberleşme", "gsm"
        ]),
        (BenchmarkSector.MediaAndMarketing,
        [
            "media", "advertising", "marketing", "public relations", "broadcast", "publishing", "entertainment",
            "newspaper", "film", "music", "gaming", "game",
            "medya", "reklam", "pazarlama", "halkla ilişkiler", "yayın", "eğlence", "gazete", "oyun"
        ]),
        (BenchmarkSector.EcommerceAndRetail,
        [
            "retail", "e-commerce", "ecommerce", "commerce", "marketplace", "wholesale", "supermarket", "grocery",
            "apparel", "fashion", "consumer goods", "fmcg",
            "perakende", "e-ticaret", "mağaza", "süpermarket", "toptan", "giyim", "moda", "tüketici ürünleri"
        ]),
        (BenchmarkSector.LogisticsAndTransport,
        [
            "logistic", "transport", "shipping", "freight", "supply chain", "airline", "aviation", "cargo", "courier",
            "warehous", "maritime",
            "lojistik", "taşımacılık", "nakliy", "kargo", "kurye", "ulaştırma", "havayolu", "denizcilik", "liman"
        ]),
        (BenchmarkSector.ConstructionAndRealEstate,
        [
            "construction", "real estate", "architecture", "civil engineering", "building materials", "property",
            "inşaat", "gayrimenkul", "emlak", "mimarlık", "yapı"
        ]),
        (BenchmarkSector.ManufacturingAndIndustry,
        [
            "manufactur", "industrial", "automotive", "machinery", "chemical", "textile", "steel", "metal", "plastics",
            "electrical equipment", "energy", "oil", "gas", "mining", "defense", "defence", "aerospace", "appliances",
            "food and beverage", "food production", "packaging",
            "üretim", "sanayi", "imalat", "otomotiv", "makine", "kimya", "tekstil", "çelik", "metal", "plastik",
            "enerji", "petrol", "maden", "savunma", "havacılık", "gıda", "ambalaj", "fabrika"
        ]),
        (BenchmarkSector.PublicAndNonProfit,
        [
            "government", "public administration", "non-profit", "nonprofit", "civic", "international affairs",
            "military", "municipal",
            "kamu", "belediye", "dernek", "vakıf", "sivil toplum", "bakanlık", "devlet"
        ]),
        (BenchmarkSector.SoftwareAndIt,
        [
            "software", "information technology", "it services", "it consulting", "computer", "internet", "technology",
            "data infrastructure", "artificial intelligence", "cybersecurity", "cloud", "saas", "digital",
            "yazılım", "bilişim", "bilgi teknoloji", "teknoloji", "siber", "dijital", "veri"
        ]),
        (BenchmarkSector.ConsultingAndProfessionalServices,
        [
            "consulting", "professional services", "staffing", "recruiting", "human resources", "accounting", "audit",
            "law", "legal", "outsourcing", "business services",
            "danışmanlık", "insan kaynakları", "muhasebe", "denetim", "hukuk", "avukat", "dış kaynak"
        ]),
    ];

    public static BenchmarkSector? Classify(string? industry)
    {
        if (string.IsNullOrWhiteSpace(industry))
        {
            return null;
        }

        var text = Normalize(industry);
        foreach (var (sector, keywords) in Rules)
        {
            foreach (var keyword in keywords)
            {
                if (text.Contains(Normalize(keyword), StringComparison.Ordinal))
                {
                    return sector;
                }
            }
        }

        return null;
    }

    private static string Normalize(string value) =>
        value.Replace('İ', 'i').Replace('I', 'i').Replace('ı', 'i').ToLowerInvariant();
}
