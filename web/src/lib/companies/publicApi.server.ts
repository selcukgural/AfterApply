// Server-side only by convention (called from server components and the sitemap); it holds no
// secret, so the `server-only` guard package is not pulled in for it.
import type {
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

/** Cached a minute per URL: an approval shows up within that, and a crawler never pins the API. */
const REVALIDATE_SECONDS = 60;

async function fetchPublic<T>(path: string, locale: string): Promise<T | null> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: { Accept: "application/json", "Accept-Language": locale },
    next: { revalidate: REVALIDATE_SECONDS },
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
  return fetchPublic<CompanyPublicResponse>(`/api/companies/public/${encodeURIComponent(slug)}`, locale);
}

export function fetchApprovedReviews(slug: string, locale: string): Promise<PagedResult<CompanyReviewPublic> | null> {
  return fetchPublic<PagedResult<CompanyReviewPublic>>(
    `/api/companies/public/${encodeURIComponent(slug)}/reviews?page=1&sort=Newest`,
    locale,
  );
}

/** Empty when the API is unreachable: the sitemap degrades to its static list, never fails. */
export async function fetchReviewedSlugs(): Promise<ReviewedCompanySlug[]> {
  try {
    return (await fetchPublic<ReviewedCompanySlug[]>("/api/companies/public/slugs", "tr")) ?? [];
  } catch {
    return [];
  }
}

/** The live configuration the scoring page quotes (prior weight, threshold). Null if unreachable —
 *  the page then prints the documented defaults and says so. */
export async function fetchCompanyReviewsConfig(): Promise<CompanyReviewsConfig | null> {
  try {
    const config = await fetchPublic<{ companyReviews?: CompanyReviewsConfig }>("/api/config", "tr");
    return config?.companyReviews ?? null;
  } catch {
    return null;
  }
}
