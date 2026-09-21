import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * What the contribute page and the company page's salary tab promised on 2026-09-16 (design
 * canvas 2B/3B). Source scans, like siteChrome.contract.test.ts: there is no render harness.
 */
const SRC = path.join(process.cwd(), "src");
const read = (relative: string) => readFileSync(path.join(SRC, relative), "utf8");

describe("the contribute page", () => {
  const page = read("app/[locale]/(protected)/contribute/page.tsx");

  it("keeps the side and the company in the URL", () => {
    expect(page).toContain("parseContributeTab(params.get(\"tab\"))");
    expect(page).toContain("params.get(\"company\")");
    expect(page).toContain("router.replace(contributeHref(");
  });

  it("switches sides with the segmented control and thanks with the banner", () => {
    expect(page).toContain("<ContributeSwitch");
    expect(page).toContain("<ContributionBanner");
    expect(page).toContain("nextSideAfterSave(");
  });

  it("offers an edit instead of a second review or experience at the same company", () => {
    expect(page).toContain("/my-reviews/${ownReview.id}/edit");
    expect(page).toContain("/my-experiences/${ownExperience.id}/edit");
  });

  it("has a third side for the candidate experience, behind its own flag", () => {
    expect(page).toContain('tab === "experience" && experiencesOn');
    expect(page).toContain("<CandidateExperienceForm");
    expect(page).toContain("createExperience");
    expect(page).toContain("config.candidateExperiences?.enabled === true");
  });

  it("is where the old write address now lands, with the slug", () => {
    const write = read("app/[locale]/(protected)/my-reviews/write/page.tsx");
    expect(write).toContain("router.replace(contributeHref(\"review\", slug))");
    // Nothing in the app links to the old address any more; only the redirect and the sign-in
    // return allowlist know it.
    for (const file of [
      "app/[locale]/(protected)/my-reviews/page.tsx",
      "components/companyReviews/CompanyReviewsSection.tsx",
      "components/companyReviews/CompanyDirectory.tsx",
    ]) {
      expect(read(file), file).not.toContain("/my-reviews/write");
    }
  });
});

describe("the company page's salary tab", () => {
  it("is a client tab over the server-rendered reviews", () => {
    const page = read("app/[locale]/(public)/companies/[slug]/page.tsx");
    expect(page).toContain("<CompanyPageTabs");
    expect(page).toContain("<CompanySalariesPanel company={company} />");
    expect(page).toContain("<CandidateExperiencesPanel company={company} />");
    expect(page).toContain("<CompanyReviewsSection company={company} initialReviews={reviews} />");
  });

  it("fetches only for a signed-in reader and sends a visitor to sign in and back", () => {
    const panel = read("components/companySalaries/CompanySalariesPanel.tsx");
    expect(panel).toContain("enabled: isAuthenticated");
    expect(panel).toContain("/login?next=");
    expect(panel).toContain("?tab=salaries");
    // The visitor's preview is sample data, marked as decoration: the real list is never public.
    expect(panel).toContain("SAMPLE_ROWS");
    expect(panel).toContain('aria-hidden="true"');
  });

  it("takes the occupation from the catalogue, never as typed text", () => {
    const form = read("components/companySalaries/CompanySalaryForm.tsx");
    expect(form).toContain("occupationsApi.search(");
    expect(form).toContain("onSelect={(option) =>");
    // Typing after a pick drops the pick — the id is cleared on every change.
    expect(form).toContain("occupationLabel: value, occupationId: null");
    expect(form).not.toContain('autoComplete="organization-title"');
  });

  it("hides the whole tab row while every client feature is off, and each tab behind its flag", () => {
    const tabs = read("components/companies/CompanyPageTabs.tsx");
    expect(tabs).toContain("config.companySalaries?.enabled");
    expect(tabs).toContain("config.candidateExperiences?.enabled");
    expect(tabs).toContain("config.companyIntelligence?.enabled");
    expect(tabs).toContain("if (!salariesOn && !experiencesOn && !intelligenceOn)");
    expect(tabs).toContain('from "@/components/layout/navLink"');
  });
});

describe("the company page's candidate-experience tab", () => {
  it("is public: fetched for anyone, no sample rows, a visitor is only sent to sign in to share", () => {
    const panel = read("components/candidateExperiences/CandidateExperiencesPanel.tsx");
    expect(panel).not.toContain("enabled: isAuthenticated,\n    queryFn: () => candidateExperiencesApi.list");
    expect(panel).toContain("queryFn: () => candidateExperiencesApi.list(company.slug, page),\n  });");
    expect(panel).not.toContain("SAMPLE_ROWS");
    expect(panel).toContain("/login?next=");
    expect(panel).toContain("?tab=experiences");
  });

  it("publishes no free text and asks nothing that points at a person", () => {
    const form = read("components/candidateExperiences/CandidateExperienceForm.tsx");
    expect(form).not.toContain("<textarea");
    expect(form).not.toContain("<Input");
    expect(form).toContain("<CategoryRatingRow");
    expect(form).toContain("<FactPills");
  });
});
