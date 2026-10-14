import { createTranslator } from "next-intl";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import type { CandidateExperiencePublic, CompanyReviewSummary } from "@/types/api";
import {
  companyPageLead,
  experienceSnippetFacts,
  experiencesPageMeta,
  isCompanyPageIndexable,
  type RootTranslate,
} from "./companyPageContent";

// 2026-10-14: 34 of the 45 company pages in the sitemap were indexed for a single candidate
// experience, yet opened on the reviews tab — the HTML a crawler read said "no published reviews
// yet" while the experience loaded only on a click. Those pages now lead with the experience.

const translator = (locale: "tr" | "en"): RootTranslate => {
  const t = createTranslator({ locale, messages: locale === "tr" ? tr : en });
  return (key, values) => t(key as never, values as never);
};

const company = (approvedCount: number, candidateExperienceCount: number) => ({
  name: "Avansas.com",
  summary: { approvedCount } as CompanyReviewSummary,
  candidateExperienceCount,
});

const experience = (overrides: Partial<CandidateExperiencePublic> = {}): CandidateExperiencePublic => ({
  id: "e1",
  overallRating: 3,
  categoryRatings: [],
  likedStatements: [],
  improvableStatements: [],
  outcome: "Rejected",
  duration: "OneToTwoWeeks",
  stages: "One",
  interviewTypes: [],
  submittedQuarter: "2026-Q4",
  helpfulCount: 0,
  backedByApplication: true,
  ...overrides,
});

describe("which half a company page leads with", () => {
  it("leads with candidate experiences only when there is no review", () => {
    expect(companyPageLead(company(0, 1))).toBe("experiences");
    expect(companyPageLead(company(1, 1))).toBe("reviews");
    expect(companyPageLead(company(1, 0))).toBe("reviews");
    expect(companyPageLead(company(0, 0))).toBe("reviews");
  });

  it("still indexes a page with one review or one experience, and not an empty one", () => {
    expect(isCompanyPageIndexable(company(0, 1))).toBe(true);
    expect(isCompanyPageIndexable(company(1, 0))).toBe(true);
    expect(isCompanyPageIndexable(company(0, 0))).toBe(false);
  });
});

describe("the search snippet of a page that leads with experiences", () => {
  it("names the newest experience's facts in Turkish", () => {
    const meta = experiencesPageMeta(company(0, 1), experience(), translator("tr"), "tr");
    expect(meta.title).toBe("Avansas.com mülakat ve işe alım süreci deneyimleri");
    expect(meta.description).toBe(
      "Avansas.com: 1 aday deneyimi. Son paylaşılan süreç: 1–2 hafta, 1 aşama, olumsuz yanıt, genel puan 3/5. Anonim ve yapılandırılmış.",
    );
  });

  it("names them in English, with the plural on the count", () => {
    const meta = experiencesPageMeta(company(0, 2), experience({ stages: "Three", outcome: "NoResponse" }), translator("en"), "en");
    expect(meta.title).toBe("Avansas.com interview and hiring process experiences");
    expect(meta.description).toBe(
      "Avansas.com: 2 candidate experiences. Latest process shared: 1–2 weeks, 3 stages, never heard back, overall 3/5. Anonymous and structured.",
    );
  });

  it("never says the page has nothing", () => {
    for (const locale of ["tr", "en"] as const) {
      const meta = experiencesPageMeta(company(0, 1), experience(), translator(locale), locale);
      expect(meta.description.toLocaleLowerCase(locale)).not.toMatch(/henüz|yok\b|no published|not yet/);
    }
  });

  it("leaves out the facts the author skipped and keeps the rating", () => {
    const facts = experienceSnippetFacts(experience({ duration: null, stages: null, outcome: null, overallRating: 1 }), translator("tr"), "tr");
    expect(facts).toEqual(["genel puan 1/5"]);
  });

  it("writes five or more stages the way the card does", () => {
    expect(experienceSnippetFacts(experience({ stages: "FivePlus" }), translator("tr"), "tr")).toContain("5+ aşama");
  });

  it("falls back to what an experience covers when the server could not read the list", () => {
    const meta = experiencesPageMeta(company(0, 1), null, translator("tr"), "tr");
    expect(meta.description).toBe("Avansas.com: 1 aday deneyimi — süreç süresi, aşama sayısı, sonuç ve puan. Anonim ve yapılandırılmış.");
  });
});
