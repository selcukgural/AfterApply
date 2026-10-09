import type { SiteTrafficCounterResponse } from "@/types/api";

/**
 * Folds the raw counter rows into the handful of numbers worth looking at on /admin/metrics.
 *
 * Note what "landingToRegister" is and is not: new accounts divided by landing-page views, both
 * counted independently over the same 30 days. Nobody was followed from one to the other — there
 * is no visitor id to follow — so it is a ratio of two totals, not a conversion rate, and a visitor
 * who lands in October and registers in November lands in both numbers anyway. At this traffic it
 * answers the only question being asked: does anybody arrive, and does anybody continue.
 *
 * New accounts are the e-mail form's and the social sign-ups' together (2026-10-02). Until then
 * only the form was counted, while most accounts came in through LinkedIn, Google or GitHub — the
 * ratio read 0% in a month that had five form sign-ups and many more social ones.
 *
 * The ratio is returned as a percentage (0–100), because that is what `formatRate` takes; handing
 * it a 0–1 fraction is how 5 sign-ups out of 556 views came out as "%0,0".
 */
/**
 * The free tools, as the counter sees them: the pages each one lives on (both languages' slugs),
 * the event that says a visitor got its result, and whether its result has a share button.
 * `resultEvent` is null for a page with nothing to run. The CV scan keeps its own event; every
 * other tool reports `ToolResultShown` and is told apart by its path (2026-10-09).
 */
export const FREE_TOOLS = [
  { key: "cvScan", paths: ["/cv-tarama", "/cv-scan"], resultEvent: "CvScanCompleted", hasShare: true },
  { key: "benchmark", paths: ["/benchmark"], resultEvent: "ToolResultShown", hasShare: true },
  {
    key: "offerCompare",
    paths: ["/teklif-karsilastirma", "/offer-comparison"],
    resultEvent: "ToolResultShown",
    hasShare: false,
  },
  { key: "responseRates", paths: ["/response-rates"], resultEvent: null, hasShare: false },
  // The "no reply" report lives on every company page, so it is counted across all of them.
  { key: "silenceReport", paths: ["/companies/"], resultEvent: "ToolResultShown", hasShare: false },
] as const;

export type FreeToolKey = (typeof FREE_TOOLS)[number]["key"];

/** A path belongs to a tool when it is one of its pages, or — for an entry ending in "/" — under it. */
function onToolPage(path: string, paths: readonly string[]): boolean {
  return paths.some((toolPath) => (toolPath.endsWith("/") ? path.startsWith(toolPath) : path === toolPath));
}

export function summariseTraffic(rows: SiteTrafficCounterResponse[] | undefined) {
  if (!rows || rows.length === 0) {
    return null;
  }

  const totalFor = (event: string) =>
    rows.filter((r) => r.event === event).reduce((sum, r) => sum + r.count, 0);

  const pageViews = rows.filter((r) => r.event === "PageView");
  const viewsOf = (path: string) => pageViews.filter((r) => r.path === path).reduce((sum, r) => sum + r.count, 0);
  const landingViews = viewsOf("/");
  const registerCompleted = totalFor("RegisterCompleted");
  const socialCompleted = totalFor("RegisterSocialCompleted");
  const newAccounts = registerCompleted + socialCompleted;

  const byKey = (source: SiteTrafficCounterResponse[], key: (r: SiteTrafficCounterResponse) => string) => {
    const totals = new Map<string, number>();
    for (const row of source) {
      totals.set(key(row), (totals.get(key(row)) ?? 0) + row.count);
    }
    return [...totals.entries()].sort((a, b) => b[1] - a[1]).slice(0, 8);
  };

  const ctaClicks = totalFor("CtaGetStarted");
  const headerCtaClicks = totalFor("CtaHeaderGetStarted");
  const heroSocialClicks = totalFor("CtaHeroSocialSignIn");
  const toolSignUpClicks = totalFor("CtaToolSignUp");

  const onPages = (event: string, paths: readonly string[]) =>
    rows.filter((r) => r.event === event && onToolPage(r.path, paths)).reduce((sum, r) => sum + r.count, 0);

  const tools = FREE_TOOLS.map((tool) => ({
    key: tool.key as FreeToolKey,
    views: onPages("PageView", tool.paths),
    results: tool.resultEvent === null ? null : onPages(tool.resultEvent, tool.paths),
    signUpClicks: onPages("CtaToolSignUp", tool.paths),
    shares: tool.hasShare ? onPages("ShareClicked", tool.paths) : null,
  }));

  return {
    pageViews: totalFor("PageView"),
    landingViews,
    cvScans: totalFor("CvScanCompleted"),
    ctaClicks,
    headerCtaClicks,
    heroSocialClicks,
    toolSignUpClicks,
    /** Every sign-up button on the public site together: in-page, header, hero social, tools. */
    signUpClicks: ctaClicks + headerCtaClicks + heroSocialClicks + toolSignUpClicks,
    registerViews: viewsOf("/register"),
    registerStarted: totalFor("RegisterStarted"),
    passwordRejected: totalFor("RegisterPasswordRejected"),
    registerCompleted,
    socialStarted: totalFor("RegisterSocialStarted"),
    socialCompleted,
    newAccounts,
    landingToRegister: landingViews === 0 ? null : (newAccounts / landingViews) * 100,
    tools,
    topPages: byKey(pageViews, (r) => r.path),
    topReferrers: byKey(pageViews, (r) => r.referrerHost),
  };
}
