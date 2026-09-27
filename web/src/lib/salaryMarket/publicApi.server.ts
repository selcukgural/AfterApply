// Server-side only by convention (called from server components and the sitemap).
import { renderHeaders } from "@/lib/api/renderHeaders.server";
import type { SalaryOccupationResponse, SalaryOccupationsResponse } from "@/types/api";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5151";

/**
 * The pages read fresh on every request, like the company pages (`lib/companies/publicApi.server.ts`):
 * the API answers from memory, and a flag switched off in the admin panel has to take the pages
 * with it now, not after a web container's own cache runs out. The sitemap's read stays on a
 * one-minute revalidate — a crawler never pins the API through it.
 */
async function fetchSalaryApi<T>(path: string, fresh: boolean): Promise<T | null> {
  const visitor = fresh ? await renderHeaders() : {};
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: { Accept: "application/json", ...visitor },
    ...(fresh ? { cache: "no-store" } : { next: { revalidate: 60 } }),
  });
  // 404: the flag is off, or no such occupation — either way the page does not exist.
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error(`Salary market API answered ${response.status} for ${path}`);
  }
  return (await response.json()) as T;
}

export function fetchSalaryOccupations(): Promise<SalaryOccupationsResponse | null> {
  return fetchSalaryApi<SalaryOccupationsResponse>("/api/salary-market/occupations", true);
}

export function fetchSalaryOccupation(slug: string): Promise<SalaryOccupationResponse | null> {
  // The API's slugs are lower-case letters, digits and dashes; anything else is not one.
  if (!/^[a-z0-9-]{1,80}$/.test(slug)) {
    return Promise.resolve(null);
  }
  return fetchSalaryApi<SalaryOccupationResponse>(`/api/salary-market/occupations/${slug}`, true);
}

/** Empty when the pages are off or the API is unreachable: the sitemap then simply lists none. */
export async function fetchSalaryOccupationsForSitemap(): Promise<SalaryOccupationsResponse | null> {
  try {
    return await fetchSalaryApi<SalaryOccupationsResponse>("/api/salary-market/occupations", false);
  } catch {
    return null;
  }
}
