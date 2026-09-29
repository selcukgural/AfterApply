import type { ClientConfigResponse } from "@/types/api";
import { isActivePath } from "@/components/layout/navLink";
import { switchedOff, withoutSwitchedOff } from "@/lib/config/switchedOff";
import { cvScanPath } from "@/lib/cvScan/path";
import { offerComparePath } from "@/lib/offerCompare/path";
import { salaryMarketPath } from "@/lib/salaryMarket/path";
import type { LandingIcon } from "@/components/landing/landingIcons";

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
  | "shareExperience"
  | "myReviews"
  | "tools"
  | "responseRates"
  | "salaryMarket"
  | "weeklyJobs"
  | "cvScan"
  | "offerCompare"
  | "benchmark"
  | "guide"
  | "blog";

export interface NavItem {
  href: string;
  key: NavKey;
  /** A rule between this item and the one before it — the group's read → write → mine seams. */
  dividerBefore?: boolean;
  /** Carries the "Pro" badge while the plan is on sale and the account is not Pro. */
  proBadge?: boolean;
  /** Every menu item is drawn with an icon and a line of description (variant A, 2026-09-29), the
   *  way the signed-out header draws its Tools menu. The line is `siteNav.toolsMenu.<key>.description`
   *  for a tool — the same words the signed-out menu shows — and `navDescriptions.<key>` otherwise. */
  icon?: LandingIcon;
}

/** One position in the signed-in row: a plain link, or a trigger with a menu under it. */
export type NavEntry = { type: "link"; href: string; key: NavKey } | { type: "group"; key: NavKey; items: NavItem[] };

export type NavFlags = Pick<
  ClientConfigResponse,
  "jobSources" | "companyReviews" | "companySalaries" | "candidateExperiences" | "blog" | "responseRates" | "salaryMarket"
> &
  Partial<Pick<ClientConfigResponse, "cvScan">>;

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
 * then mine (one page for all three kinds since 2026-09-18) — and the tools are their own. "New application" is not here: it is the header's
 * primary button, rendered by NavBar on every page.
 *
 * Order (2026-09-20): the menus first, the plain links after them — dashboard, then the three
 * groups, then "My CVs" and "Blog". Two links with nothing under them read better side by side
 * at the end of the row than wedged between two triggers, and it is the same for the drawer.
 * The blog is a link of its own, not a tool: it is read, not used, and the signed-out header
 * (SiteHeader) already carries it at the top level — a member should find it in the same place.
 *
 * Flags: the weekly postings and the company pages ship dark, so their items follow the server
 * config and appear only once it says the routes exist — the same rule the pages themselves use.
 * The blog follows a stricter one: its link appears only once there is a published post to read
 * (`blog.hasPublishedPosts`), because a "Blog" that opens on nothing is worse than no blog.
 *
 * Runtime switches (2026-09-27): the guide and the CV scan go once the server says their flag is
 * off (`switchedOff`). `loaded` is false while the config has not answered — the built-in default
 * then stands in for it and must not be read as "switched off".
 */
export function buildNavEntries(flags: NavFlags, locale: string, loaded = true): NavEntry[] {
  const reviewsOn = flags.companyReviews?.enabled === true;
  const salariesOn = flags.companySalaries?.enabled === true;
  const experiencesOn = flags.candidateExperiences?.enabled === true;
  const weeklyJobsOn = flags.jobSources?.enabled === true;
  const blogOn = flags.blog?.enabled === true && flags.blog.hasPublishedPosts === true;
  const responseRatesOn = flags.responseRates?.enabled === true;
  const salaryMarketOn = flags.salaryMarket?.enabled === true;
  const off = switchedOff(loaded ? flags : null);

  const companies: NavItem[] = [
    { href: "/companies", key: "allCompanies", icon: "companies" },
    { href: "/contribute?tab=review", key: "writeReview", dividerBefore: true, icon: "review" },
    ...(salariesOn ? [{ href: "/contribute?tab=salary", key: "shareSalary", icon: "salary" } as NavItem] : []),
    ...(experiencesOn ? [{ href: "/contribute?tab=experience", key: "shareExperience", icon: "experience" } as NavItem] : []),
    // One "mine" page since 2026-09-18: reviews, salaries and experiences are one list there.
    { href: "/my-reviews", key: "myReviews", dividerBefore: true, icon: "person" },
  ];

  const tools: NavItem[] = withoutSwitchedOff<NavItem>([
    ...(weeklyJobsOn ? [{ href: "/weekly-jobs", key: "weeklyJobs", proBadge: true, icon: "weeklyJobs" } as NavItem] : []),
    // The sector table reads like a tool (2026-09-22): a public number to look up, not a place to
    // contribute — so it sits with the scan and the benchmark rather than in the companies group.
    ...(responseRatesOn ? [{ href: "/response-rates", key: "responseRates", icon: "analytics" } as NavItem] : []),
    // The survey salary pages (2026-09-27): public figures to look up, like the sector table.
    ...(salaryMarketOn ? [{ href: salaryMarketPath(locale), key: "salaryMarket", icon: "salary" } as NavItem] : []),
    { href: cvScanPath(locale), key: "cvScan", icon: "cv" },
    { href: "/benchmark", key: "benchmark", icon: "analytics" },
    { href: offerComparePath(locale), key: "offerCompare", icon: "offer" },
    { href: "/guide", key: "guide", icon: "guide" },
  ], off);

  return [
    { type: "link", href: "/dashboard", key: "dashboard" },
    {
      type: "group",
      key: "applicationsMenu",
      items: [
        { href: "/applications", key: "allApplications", icon: "tracking" },
        { href: "/tracked-jobs", key: "trackedJobs", icon: "bookmark" },
        { href: "/import", key: "import", icon: "import" },
      ],
    },
    ...(reviewsOn ? [{ type: "group", key: "companies", items: companies } as NavEntry] : []),
    { type: "group", key: "tools", items: tools },
    { type: "link", href: "/cv", key: "cv" },
    ...(blogOn ? [{ type: "link", href: "/blog", key: "blog" } as NavEntry] : []),
  ];
}

/**
 * Whether a menu item is the page being viewed. A contribute link differs from its twin only by
 * query string, which the pathname does not carry, so neither of those is ever "current".
 */
export function isNavItemActive(pathname: string, href: string): boolean {
  return !href.includes("?") && isActivePath(pathname, href);
}
