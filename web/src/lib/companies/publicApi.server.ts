// Server-side only by convention (called from server components and the sitemap). The one secret a
// fetch carries comes from renderHeaders, which reads it from a server-only environment variable.
import { cache } from "react";
import { renderHeaders } from "@/lib/api/renderHeaders.server";
import type { CompanyTabFeatures } from "@/lib/companies/companyPageContent";
import type {
  CandidateExperiencePage,
  ClientConfigResponse,
  CompanyPublicResponse,
  CompanyReviewPublic,
  CompanyReviewsConfig,
  PagedResult,
  ReviewedCompanySlug,
} from "@/types/api";

/**
 * The public company pages are the first pages on this site rendered with data on the server: the
 * reviews have to be in the HTML for a search engine (and for someone opening the link with no
 * account) to see anything at all. Same base URL the browser client uses; no token — these routes
 * are anonymous by design.
 */
const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5151";

/**
 * Two kinds of fetch. The company page and its reviews are read fresh on every request
 * (`no-store`): the API caches them itself, in Redis, shared by every API instance and evicted
 * the moment anything on the page changes — whereas Next's own data cache is per web container
 * (`output: "standalone"`, no cache handler), so a minute here was a minute of staleness that no
 * write could cut short, on top of whatever the API's cache did. The slug list and the config
 * stay on a one-minute revalidate: they feed the sitemap and a static-ish page, a crawler never
 * pins the API through them, and a minute's lag there is invisible.
 */
const REVALIDATE_SECONDS = 60;

type Freshness = "fresh" | "revalidate";

async function fetchPublic<T>(path: string, locale: string, freshness: Freshness): Promise<T | null> {
  // A fresh fetch runs per request, so it names the visitor it renders for (see renderHeaders).
  const visitor = freshness === "fresh" ? await renderHeaders() : {};
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: { Accept: "application/json", "Accept-Language": locale, ...visitor },
    ...(freshness === "fresh" ? { cache: "no-store" } : { next: { revalidate: REVALIDATE_SECONDS } }),
  });
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error(`Public company API answered ${response.status} for ${path}`);
  }
  return (await response.json()) as T;
}

export function fetchCompanyBySlug(slug: string, locale: string): Promise<CompanyPublicResponse | null> {
  return fetchPublic<CompanyPublicResponse>(`/api/companies/public/${encodeURIComponent(slug)}`, locale, "fresh");
}

export function fetchApprovedReviews(slug: string, locale: string): Promise<PagedResult<CompanyReviewPublic> | null> {
  return fetchPublic<PagedResult<CompanyReviewPublic>>(
    `/api/companies/public/${encodeURIComponent(slug)}/reviews?page=1&sort=Newest`,
    locale,
    "fresh",
  );
}

/**
 * The first page of a company's candidate experiences, newest first, so a company known for them
 * has them in its HTML (2026-10-14: the tab used to fetch them in the browser only, and a crawler
 * read "no reviews yet" on a page that had an experience). Null when the feature is off (the API
 * answers 404) or the API failed — the tab then fetches on the client as before, the page itself
 * never fails over it. `cache` lets the metadata and the page share one request.
 */
export const fetchCandidateExperiences = cache(async (slug: string, locale: string): Promise<CandidateExperiencePage | null> => {
  try {
    return await fetchPublic<CandidateExperiencePage>(`/api/companies/public/${encodeURIComponent(slug)}/experiences?page=1`, locale, "fresh");
  } catch {
    return null;
  }
});

/** Empty when the API is unreachable: the sitemap degrades to its static list, never fails. */
export async function fetchReviewedSlugs(): Promise<ReviewedCompanySlug[]> {
  try {
    return (await fetchPublic<ReviewedCompanySlug[]>("/api/companies/public/slugs", "tr", "revalidate")) ?? [];
  } catch {
    return [];
  }
}

/** The live configuration the scoring page quotes (prior weight, threshold). Null if unreachable —
 *  the page then prints the documented defaults and says so. */
export async function fetchCompanyReviewsConfig(): Promise<CompanyReviewsConfig | null> {
  try {
    const config = await fetchPublic<{ companyReviews?: CompanyReviewsConfig }>("/api/config", "tr", "revalidate");
    return config?.companyReviews ?? null;
  } catch {
    return null;
  }
}

/**
 * Which company-page tabs are switched on, read on the server so the tab row is in the HTML the
 * way the browser will show it. Null if the config is unreachable: the tabs then wait for the
 * browser's own config read, as they did before.
 */
export async function fetchCompanyTabFeatures(): Promise<CompanyTabFeatures | null> {
  try {
    const config = await fetchPublic<ClientConfigResponse>("/api/config", "tr", "revalidate");
    return config === null
      ? null
      : {
          salaries: config.companySalaries?.enabled === true,
          experiences: config.candidateExperiences?.enabled === true,
          intelligence: config.companyIntelligence?.enabled === true,
        };
  } catch {
    return null;
  }
}
