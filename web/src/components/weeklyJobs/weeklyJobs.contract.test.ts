import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";

/**
 * What the weekly job matching promised when it was built (2026-09-14, direction A of the design
 * canvas). Source scans, like siteChrome.contract.test.ts: there is no render harness, and each
 * rule is one a screenshot review would otherwise re-check by hand.
 */
const SRC = path.join(process.cwd(), "src");
const read = (relative: string) => readFileSync(path.join(SRC, relative), "utf8");

describe("the weekly jobs pages", () => {
  it("exist only when the server flag says so — nav link and pages alike", () => {
    // The nav item comes from navGroups.ts, which the navbar feeds with the client config.
    expect(read("components/layout/navGroups.ts")).toContain("flags.jobSources?.enabled === true");
    expect(read("components/layout/NavBar.tsx")).toContain("buildNavEntries(config)");
    const access = read("components/weeklyJobs/useWeeklyJobsAccess.ts");
    expect(access).toContain('router.replace("/dashboard")');
    expect(access).toContain("enabled: isLoaded && enabled");
    for (const page of ["weekly-jobs/page.tsx", "weekly-jobs/[postingId]/page.tsx", "weekly-jobs/criteria/page.tsx"]) {
      expect(read(`app/[locale]/(protected)/${page}`), page).toContain("useWeeklyJobsAccess");
    }
  });

  it("renders scraped and model-written text as text, never as HTML", () => {
    for (const file of ["components/weeklyJobs/PostingDetail.tsx", "components/weeklyJobs/PostingList.tsx"]) {
      expect(read(file), file).not.toContain("dangerouslySetInnerHTML");
    }
    expect(read("components/weeklyJobs/PostingDetail.tsx")).toContain('rel="noopener noreferrer"');
  });

  it("asks for the AI-scoring consent with a box that is never pre-ticked, linking to the privacy section", () => {
    const form = read("components/weeklyJobs/CriteriaForm.tsx");
    expect(form).toContain("useState(false)");
    expect(form).toContain('href="/privacy#job-matching"');
    expect(read("app/[locale]/(public)/privacy/page.tsx")).toContain('<section id="job-matching">');
  });

  it("is a real link per row so a phone lands on the posting's own page", () => {
    const list = read("components/weeklyJobs/PostingList.tsx");
    expect(list).toContain("href={`/weekly-jobs/${item.id}`}");
    expect(list).toContain("window.matchMedia(DESKTOP_QUERY)");
  });

  it("ships its copy in both languages", () => {
    const keys = (o: unknown, prefix = ""): string[] =>
      typeof o === "object" && o !== null
        ? Object.entries(o).flatMap(([k, v]) => keys(v, prefix ? `${prefix}.${k}` : k))
        : [prefix];
    expect(keys(tr.weeklyJobs).sort()).toEqual(keys(en.weeklyJobs).sort());
    expect(keys(tr.privacy.jobMatching).sort()).toEqual(keys(en.privacy.jobMatching).sort());
    expect(tr.nav.weeklyJobs).toBeTruthy();
    expect(en.nav.weeklyJobs).toBeTruthy();
  });

  it("names the one exception to the CVs page's 'never sent to an AI service' promise wherever that promise is made", () => {
    for (const messages of [tr, en]) {
      expect(messages.privacy.cvStorage.noTransfer).toMatch(/Vertex|Haftalık ilan eşleştirme|Weekly job matching/);
      expect(messages.cv.consent.after).toMatch(/ilan eşleştirme|job matching/);
    }
  });
});

describe("how the feature is explained", () => {
  // 2026-09-16: three surfaces said "how it works"; two sent the reader to /weekly-jobs — the
  // product page, which for a signed-out visitor is a login redirect — and the gate page had no
  // way to the help topic at all. "How it works" is the help topic, everywhere.
  it("sends every 'how it works' link to the help topic", () => {
    expect(read("components/landing/WeeklyJobsFeatureCard.tsx")).toContain('href="/help/weekly-jobs"');
    expect(read("components/landing/WeeklyJobsFeatureCard.tsx")).not.toContain('href="/weekly-jobs"');
    expect(read("components/dashboard/WeeklyJobsAnnouncement.tsx")).toContain('href="/help/weekly-jobs"');
    expect(read("components/weeklyJobs/ProGate.tsx")).toContain('href="/help/weekly-jobs"');
  });

  it("labels the gate page's help link in both languages", () => {
    for (const messages of [tr, en]) {
      expect(messages.weeklyJobs.gate.helpLink.length).toBeGreaterThan(0);
    }
  });
});

describe("the checkout form", () => {
  // 2026-09-16 browser round: the second checkout started from an empty address and phone, and
  // the server's per-field messages arrived as one red line under the consent box.
  it("starts from the user's last billing details and shows validation under each field", () => {
    const page = read("app/[locale]/(protected)/pro/checkout/page.tsx");
    expect(page).toContain("paymentsApi.getBillingDefaults");
    expect(page).toContain("fieldErrorsOf(err)");
    const form = read("components/pro/BillingForm.tsx");
    for (const field of ["billingName", "billingAddress", "billingPhone"]) {
      expect(form).toContain(`error={fieldErrors.${field}}`);
    }
  });

  it("tells the payer what was charged, not what the plan cost when the order was opened", () => {
    expect(read("app/[locale]/(protected)/pro/orders/[orderId]/page.tsx")).toContain(
      "data.chargedAmountMinor ?? data.amountMinor",
    );
  });
});
