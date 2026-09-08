import type { ApplicationListSortBy, ApplicationStatus, CompanyGroupSortBy, SortDirection } from "@/types/api";
import { APPLICATION_STATUSES } from "@/lib/constants/applicationStatus";

/**
 * Which shape the applications list is in: one row per application, or one group per company.
 *
 * It lives in the URL rather than in component state so the view survives a reload, the back button
 * and a pasted link — the same reason the filters already do. "flat" is the default, so today's
 * link to /applications keeps opening today's list.
 */
export type ListView = "flat" | "company";

export const FLAT_SORT_OPTIONS: readonly ApplicationListSortBy[] = [
  "AppliedAt",
  "UpdatedAt",
  "CompanyName",
  "JobTitle",
  "Status",
];

/** The company view orders whole groups, so half of the flat list's options have no meaning here —
 *  a company holding five applications has no one job title, status or applied date. */
export const COMPANY_SORT_OPTIONS: readonly CompanyGroupSortBy[] = [
  "LastActivity",
  "ApplicationCount",
  "CompanyName",
];

export function parseView(raw: string | null): ListView {
  return raw === "company" ? "company" : "flat";
}

/**
 * Every one of these validates rather than casts, because the two views share the `sortBy`
 * parameter: switching from the flat list to the company view with `sortBy=AppliedAt` still in the
 * URL would otherwise send a value the grouped endpoint rejects, and the user would get an error
 * for pressing a toggle. An unrecognised value falls back to the view's default.
 */
export function parseFlatSortBy(raw: string | null): ApplicationListSortBy {
  return FLAT_SORT_OPTIONS.find((option) => option === raw) ?? "AppliedAt";
}

export function parseCompanySortBy(raw: string | null): CompanyGroupSortBy {
  return COMPANY_SORT_OPTIONS.find((option) => option === raw) ?? "LastActivity";
}

export function parseSortDirection(raw: string | null): SortDirection {
  return raw === "Ascending" ? "Ascending" : "Descending";
}

export function parseStatus(raw: string | null): ApplicationStatus | "" {
  return APPLICATION_STATUSES.find((status) => status === raw) ?? "";
}

export function parsePage(raw: string | null): number {
  const page = Number(raw);
  return Number.isInteger(page) && page >= 1 ? page : 1;
}

/**
 * A group bigger than this starts folded in the company view.
 *
 * Groups open expanded, because the reason to be on this screen is to see the applications and a
 * page of ten collapsed company names shows none of them. But one company with twenty applications
 * fills the page on its own and pushes every other company below the fold — which is exactly the
 * "I can't see my companies" problem this view exists to fix. Past this many rows the header alone
 * (count, status breakdown, last activity) says more than the rows would, and opening it is one
 * click.
 */
export const COLLAPSE_GROUPS_LARGER_THAN = 5;

/** Which companies on a page start folded. */
export function initiallyCollapsed(
  groups: readonly { companyId: string; applications: readonly unknown[] }[],
): string[] {
  return groups
    .filter((group) => group.applications.length > COLLAPSE_GROUPS_LARGER_THAN)
    .map((group) => group.companyId);
}

/** Identifies the set of groups on screen, so the fold state can be reset when the page moves under
 *  it — a company id carried over from the previous page would fold the wrong row. */
export function groupPageKey(groups: readonly { companyId: string }[]): string {
  return groups.map((group) => group.companyId).join(",");
}
