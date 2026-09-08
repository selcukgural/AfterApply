import type {
  BenchmarkLocation,
  BenchmarkPeriod,
  BenchmarkSector,
  BenchmarkSeniority,
} from "@/types/api";

/**
 * The option lists the benchmark form offers, in the order they should be read rather than the
 * order they happen to be declared in the API's enums.
 *
 * Kept out of the form component so they can be checked without a DOM. Two things go wrong here
 * silently and neither shows up in a type error: a value the API does not know comes back as a 400
 * the visitor reads as "could not be saved", and a value with no entry in a message catalogue
 * renders as the raw key ("sectors.Telecom"). `options.test.ts` guards both.
 */

export const BENCHMARK_SECTORS: BenchmarkSector[] = [
  "SoftwareAndIt",
  "FinanceAndInsurance",
  "EcommerceAndRetail",
  "ManufacturingAndIndustry",
  "Telecom",
  "HealthAndPharma",
  "Education",
  "ConsultingAndProfessionalServices",
  "MediaAndMarketing",
  "LogisticsAndTransport",
  "ConstructionAndRealEstate",
  "PublicAndNonProfit",
  "Other",
];

export const BENCHMARK_PERIODS: BenchmarkPeriod[] = [
  "LastThreeMonths",
  "LastSixMonths",
  "LastTwelveMonths",
  "Longer",
];

export const BENCHMARK_SENIORITIES: BenchmarkSeniority[] = [
  "StudentOrIntern",
  "Junior",
  "Mid",
  "Senior",
  "LeadOrAbove",
];

export const BENCHMARK_LOCATIONS: BenchmarkLocation[] = [
  "Istanbul",
  "Ankara",
  "Izmir",
  "TurkeyOther",
  "Abroad",
  "Remote",
];
