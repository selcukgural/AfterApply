import type { OccupationSearchResult } from "@/types/api";
import { apiFetch } from "./httpClient";

/** The occupation catalogue typeahead. Signed-in; both names are matched whatever was typed. */
export const occupationsApi = {
  search: (q: string) => apiFetch<OccupationSearchResult[]>(`/api/occupations/search?q=${encodeURIComponent(q)}`),
};
