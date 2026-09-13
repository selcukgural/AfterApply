import type {
  CompanyPublicListItem,
  CompanyPublicResponse,
  CompanyReviewPublic,
  CompanySearchResult,
  PagedResult,
  PublicReviewSort,
  ResolvedCompany,
} from "@/types/api";
import { apiFetch } from "./httpClient";

export const companiesApi = {
  search: (q: string) => apiFetch<CompanySearchResult[]>(`/api/companies/search?q=${encodeURIComponent(q)}`),

  /** Find-or-create by name, for a company that has no page yet. Signed-in only. */
  resolve: (name: string) =>
    apiFetch<ResolvedCompany>("/api/companies/resolve", { method: "POST", body: JSON.stringify({ name }) }),

  // The public directory. These answer without a token; a stale one the browser attaches is
  // ignored by the server rather than refused, so they are safe to call from a public page.
  listPublic: (q: string, page: number) =>
    apiFetch<PagedResult<CompanyPublicListItem>>(
      `/api/companies/public?q=${encodeURIComponent(q)}&page=${page}`,
    ),

  getPublic: (slug: string) => apiFetch<CompanyPublicResponse>(`/api/companies/public/${encodeURIComponent(slug)}`),

  listPublicReviews: (slug: string, page: number, sort: PublicReviewSort) =>
    apiFetch<PagedResult<CompanyReviewPublic>>(
      `/api/companies/public/${encodeURIComponent(slug)}/reviews?page=${page}&sort=${sort}`,
    ),
};
