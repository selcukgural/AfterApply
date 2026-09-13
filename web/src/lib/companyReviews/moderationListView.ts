import type { ReviewModerationStatus } from "@/types/api";

/**
 * The moderation queue's filters live in the URL, like the applications list's: a reload, the back
 * button and a pasted link all land on the same view. Every parser validates rather than casts.
 */
export const MODERATION_STATUSES: readonly ReviewModerationStatus[] = ["Pending", "Approved", "Rejected"];

export interface ModerationListFilters {
  status: ReviewModerationStatus | "";
  company: string;
  /** yyyy-MM-dd or "" — what a date input holds. */
  from: string;
  to: string;
  page: number;
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

export function parseModerationStatus(raw: string | null): ReviewModerationStatus | "" {
  return MODERATION_STATUSES.find((status) => status === raw) ?? "";
}

export function parseDate(raw: string | null): string {
  return raw && ISO_DATE.test(raw) ? raw : "";
}

export function parsePage(raw: string | null): number {
  const page = Number(raw);
  return Number.isInteger(page) && page >= 1 ? page : 1;
}

export function parseModerationFilters(params: URLSearchParams): ModerationListFilters {
  return {
    status: parseModerationStatus(params.get("status")),
    company: (params.get("company") ?? "").slice(0, 100),
    from: parseDate(params.get("from")),
    to: parseDate(params.get("to")),
    page: parsePage(params.get("page")),
  };
}

/** Changing any filter resets the page: page 4 of a different list is not a place. */
export function withFilterChange(
  current: ModerationListFilters,
  change: Partial<Omit<ModerationListFilters, "page">>,
): ModerationListFilters {
  return { ...current, ...change, page: 1 };
}

/** The query string for the API. A date-only "to" means the whole of that day. */
export function buildModerationQueryString(filters: ModerationListFilters): string {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  if (filters.company.trim()) params.set("company", filters.company.trim());
  if (filters.from) params.set("from", `${filters.from}T00:00:00Z`);
  if (filters.to) params.set("to", `${filters.to}T23:59:59Z`);
  if (filters.page > 1) params.set("page", String(filters.page));
  const query = params.toString();
  return query ? `?${query}` : "";
}

/** The same filters as URL search params for the page itself (dates stay date-only). */
export function toSearchParams(filters: ModerationListFilters): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  if (filters.company) params.set("company", filters.company);
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  if (filters.page > 1) params.set("page", String(filters.page));
  return params;
}
