import type { BenchmarkPeriod, CompanyIntelligenceResponse, SectorResponseRatesResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

/** Both routes are anonymous; the token, when there is one, changes nothing in the answer. */
export const responseRatesApi = {
  sectors: (period: Exclude<BenchmarkPeriod, "Longer">) =>
    apiFetch<SectorResponseRatesResponse>(`/api/response-rates/sectors?period=${period}`),

  company: (companyId: string) => apiFetch<CompanyIntelligenceResponse>(`/api/company-intelligence/${companyId}`),
};
