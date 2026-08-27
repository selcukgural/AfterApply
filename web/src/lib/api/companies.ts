import type { CompanySearchResult } from "@/types/api";
import { apiFetch } from "./httpClient";

export const companiesApi = {
  search: (q: string) => apiFetch<CompanySearchResult[]>(`/api/companies/search?q=${encodeURIComponent(q)}`),
};
