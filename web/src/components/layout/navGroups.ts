import type { ClientConfigResponse } from "@/types/api";
import { isActivePath } from "@/components/layout/navLink";

/** A `nav.*` catalogue key. */
export type NavKey =
  | "dashboard"
  | "applicationsMenu"
  | "allApplications"
  | "trackedJobs"
  | "import"
  | "cv"
  | "companies"
  | "allCompanies"
  | "writeReview"
  | "shareSalary"
  | "myReviews"
  | "mySalaries"
  | "tools"
  | "weeklyJobs"
  | "cvScan"
  | "benchmark"
  | "guide";

export interface NavItem {
  href: string;
  key: NavKey;
  /** A rule between this item and the one before it — the group's read → write → mine seams. */
  dividerBefore?: boolean;
  /** Carries the "Pro" badge while the plan is on sale and the account is not Pro. */
  proBadge?: boolean;
}

/** One position in the signed-in row: a plain link, or a trigger with a menu under it. */
export type NavEntry = { type: "link"; href: string; key: NavKey } | { type: "group"; key: NavKey; items: NavItem[] };

export type NavFlags = Pick<ClientConfigResponse, "jobSources" | "companyReviews" | "companySalaries">;

/**
 * The signed-in navigation, as data. The desktop row and the mobile drawer both render from this
 * one list, in this order, so the two can no longer drift apart — which is what they had done by
 * 2026-09-17: the drawer had no "Explore", two headings the row never showed ("Companies",
 * "Free tools"), and no "New application" at all.
 *
 * Grouped by *object*, not by whether an item reads or writes (2026-09-17, variant A on the
 * navigation canvas): the previous "Explore" menu put the company directory, the two contribution
 * forms and the account-free tools in one flat list, while the author's own reviews and salaries
 * sat in the avatar menu. Now everything about companies is one group — browse, then contribute,
 * then mine — and the tools are their own. "New application" is not here: it is the header's
 * primary button, rendered by NavBar on every page.
 *
 * Flags: the weekly postings and the company pages ship dark, so their items follow the server
 * config and appear only once it says the routes exist — the same rule the pages themselves use.
 */
export function buildNavEntries(flags: NavFlags): NavEntry[] {
  const reviewsOn = flags.companyReviews?.enabled === true;
  const salariesOn = flags.companySalaries?.enabled === true;
  const weeklyJobsOn = flags.jobSources?.enabled === true;

  const companies: NavItem[] = [
    { href: "/companies", key: "allCompanies" },
    { href: "/contribute?tab=review", key: "writeReview", dividerBefore: true },
    ...(salariesOn ? [{ href: "/contribute?tab=salary", key: "shareSalary" } as NavItem] : []),
    { href: "/my-reviews", key: "myReviews", dividerBefore: true },
    ...(salariesOn ? [{ href: "/my-salaries", key: "mySalaries" } as NavItem] : []),
  ];

  const tools: NavItem[] = [
    ...(weeklyJobsOn ? [{ href: "/weekly-jobs", key: "weeklyJobs", proBadge: true } as NavItem] : []),
    { href: "/cv-tarama", key: "cvScan" },
    { href: "/benchmark", key: "benchmark" },
    { href: "/guide", key: "guide" },
  ];

  return [
    { type: "link", href: "/dashboard", key: "dashboard" },
    {
      type: "group",
      key: "applicationsMenu",
      items: [
        { href: "/applications", key: "allApplications" },
        { href: "/tracked-jobs", key: "trackedJobs" },
        { href: "/import", key: "import" },
      ],
    },
    { type: "link", href: "/cv", key: "cv" },
    ...(reviewsOn ? [{ type: "group", key: "companies", items: companies } as NavEntry] : []),
    { type: "group", key: "tools", items: tools },
  ];
}

/**
 * Whether a menu item is the page being viewed. A contribute link differs from its twin only by
 * query string, which the pathname does not carry, so neither of those is ever "current".
 */
export function isNavItemActive(pathname: string, href: string): boolean {
  return !href.includes("?") && isActivePath(pathname, href);
}
