import type { AbstractIntlMessages } from "next-intl";

/**
 * Which part of the message catalogue each layout hands to the browser.
 *
 * `NextIntlClientProvider` with no `messages` prop ships the whole catalogue — every namespace,
 * admin moderation strings and the full privacy policy included — inlined into the HTML of every
 * page. On 2026-09-14 that was 190 KB of the landing page's 295 KB, and the largest single reason
 * its mobile LCP was 4.9 s. Server components read messages through `getTranslations` and need
 * none of this; only client components (`useTranslations`) do, so each layout provides exactly the
 * namespaces its client components can reach:
 *
 *  - ROOT: the chrome every page shares (header, footer, theme switcher, the 404 page).
 *  - LANDING: root + the landing sections and the CV dropzone in the hero.
 *  - PUBLIC: root + everything a signed-out page can mount (auth forms, the tools, company pages).
 *  - The signed-in layout provides the full catalogue: it is behind a login and its size is not
 *    what a first-time visitor waits for.
 *
 * `messageScopes.test.ts` walks the import graph from each layout's entry points and fails when a
 * client component asks for a namespace its scope does not carry — the failure mode otherwise is
 * next-intl rendering the raw key on a public page, silently.
 *
 * Entries may be dotted ("help.sidebar") to take one branch of a large namespace.
 */
export const ROOT_MESSAGE_SCOPE = [
  "common",
  "nav",
  "siteNav",
  "theme",
  "notFound",
  // The header's "Scan your CV" button.
  "cvScan.navCta",
  // The share row's two labels ("copy link" / "copied") — the row sits on the CV result, which
  // the landing hero can show, and on public pages.
  "share",
] as const;

// The landing page shows sample dashboard cards, a sample benchmark result and a sample company
// summary — real components, not pictures (DECISIONS.md 2026-09-12) — so it needs their strings.
export const LANDING_MESSAGE_SCOPE = [
  ...ROOT_MESSAGE_SCOPE,
  "landing",
  "cvScan",
  "employmentStatus",
  "dashboard.breakdown",
  "dashboard.outcome",
  "dashboard.funnel",
  "dashboard.responseTime",
  "status",
  "benchmark",
  "companies.summary",
  "companies.scoring",
  // The category names only: the statement catalogue's 400 strings stay out of the landing
  // bundle (the mock's chips carry landing copy, and its "most picked" lists are empty).
  "companyReviews.categories",
] as const;

export const PUBLIC_MESSAGE_SCOPE = [
  ...ROOT_MESSAGE_SCOPE,
  "auth",
  "validation",
  "pair",
  "benchmark",
  "cvScan",
  "employmentStatus",
  "companies",
  "companyReviews",
  // The company page's salary tab: its rows name the employment type, status and currency.
  "companySalaries",
  "salaryEmploymentStatus",
  "salaryCurrency",
  "employmentType",
  // The company page's candidate-experience tab: its cards and summary name the outcome, the
  // duration and stage bands and the interview types.
  "candidateExperiences",
  "hiringOutcome",
  "processDuration",
  "stageCount",
  "interviewType",
  "reviewReportReason",
  "reviewModerationStatus",
  "help.sidebar",
  // The company directory pages with the applications list's pager.
  "applications.pagination",
  // The distance-sales terms and the refund policy, linked from the checkout and the footer.
  "termsOfSale",
  "refundPolicy",
] as const;

type Tree = { [key: string]: unknown };

/**
 * The subset of `messages` under the given (possibly dotted) paths, keeping the tree shape
 * next-intl expects. A path that does not exist is skipped rather than thrown on: a scope listing
 * a namespace the other locale lacks is the catalogue-parity test's job to catch.
 */
export function pickMessages(messages: AbstractIntlMessages, paths: readonly string[]): AbstractIntlMessages {
  const result: Tree = {};
  for (const path of paths) {
    const segments = path.split(".");
    let source: unknown = messages;
    for (const segment of segments) {
      source = source && typeof source === "object" ? (source as Tree)[segment] : undefined;
    }
    if (source === undefined) continue;

    let target = result;
    segments.slice(0, -1).forEach((segment) => {
      const existing = target[segment];
      target = existing && typeof existing === "object" ? (existing as Tree) : (target[segment] = {});
    });
    target[segments[segments.length - 1]] = source;
  }
  return result as AbstractIntlMessages;
}
