import { parsePage } from "@/lib/companyReviews/moderationListView";

/**
 * The admin salary and experience tables' filters live in the URL, like the moderation queue's:
 * a reload, the back button and a pasted link all land on the same view. Two fields only — the
 * rows have no status to filter on, and the company name is how a particular row is found.
 */
export interface ContributionListFilters {
  company: string;
  page: number;
}

export function parseContributionFilters(params: URLSearchParams): ContributionListFilters {
  return {
    company: (params.get("company") ?? "").slice(0, 100),
    page: parsePage(params.get("page")),
  };
}

/** Changing the company resets the page: page 4 of a different list is not a place. */
export function withCompanyChange(current: ContributionListFilters, company: string): ContributionListFilters {
  return { ...current, company, page: 1 };
}

/** The query string for the API. */
export function buildContributionQueryString(filters: ContributionListFilters): string {
  const params = toContributionSearchParams(filters);
  const query = params.toString();
  return query ? `?${query}` : "";
}

/** The same filters as URL search params for the page itself. */
export function toContributionSearchParams(filters: ContributionListFilters): URLSearchParams {
  const params = new URLSearchParams();
  const company = filters.company.trim();
  if (company) params.set("company", company);
  if (filters.page > 1) params.set("page", String(filters.page));
  return params;
}
