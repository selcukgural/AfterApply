import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Source scan, like the other contract tests — there is no render harness. The "tracked
 * application" label (2026-09-24): which card wears which kind, that the explanation line only
 * appears next to a chip, and that the copy states the rule without ever claiming verification.
 */
const SRC = path.join(process.cwd(), "src");
const read = (relative: string) => readFileSync(path.join(SRC, relative), "utf8");
const messages = (locale: string) => JSON.parse(readFileSync(path.join(process.cwd(), "messages", `${locale}.json`), "utf8"));

describe("the proof label on the public cards", () => {
  it("an experience wears the tracked kind, a salary and a review the accepted kind, each only when the record says so", () => {
    const experience = read("components/candidateExperiences/ExperienceCard.tsx");
    expect(experience).toContain('experience.backedByApplication ? <ProofChip kind="tracked" />');
    const salary = read("components/companySalaries/SalaryRow.tsx");
    expect(salary).toContain('entry.backedByApplication ? <ProofChip kind="accepted" />');
    const review = read("components/companyReviews/ReviewCard.tsx");
    expect(review).toContain('review.backedByApplication ? <ProofChip kind="accepted" />');
  });

  it("each tab explains the chip once, and only when one is on the page", () => {
    for (const [file, list, kind] of [
      ["components/candidateExperiences/CandidateExperiencesPanel.tsx", "list", "tracked"],
      ["components/companySalaries/CompanySalariesPanel.tsx", "list", "accepted"],
      ["components/companyReviews/CompanyReviewsSection.tsx", "reviews", "accepted"],
    ] as const) {
      expect(read(file)).toContain(`{${list}?.items.some((item) => item.backedByApplication) ? <ProofNote kind="${kind}" /> : null}`);
    }
  });
});

describe("the proof label on the author's own cards", () => {
  it("each company card shows whether its row carries the label, from the contributions list", () => {
    for (const [file, kind] of [
      ["MyExperienceCard", "tracked"],
      ["MySalaryCard", "accepted"],
      ["MyReviewCard", "accepted"],
    ] as const) {
      expect(read(`components/contributions/${file}.tsx`)).toContain(`<MyProofLine kind="${kind}" backed={backed} />`);
    }
    const page = read("app/[locale]/(protected)/my-reviews/page.tsx");
    expect(page.match(/backed=\{item\.backedByApplication\}/g)).toHaveLength(3);
  });
});

describe("the proof label's copy", () => {
  for (const locale of ["tr", "en"]) {
    it(`${locale}: states the 14 days and never claims verification`, () => {
      const m = messages(locale);
      const all = JSON.stringify([
        m.contributionProof,
        m.privacy.candidateExperiences.proof,
        m.privacy.companySalaries.proof,
        m.privacy.companyReviews.proof,
        m.help.candidateExperiences.proof,
        m.help.companySalaries.proof,
        m.help.companyReviews.proof,
      ]);
      expect(all).not.toMatch(/doğrulanmış|doğrulandı|\bverified\b/i);
      for (const text of [m.contributionProof.note.tracked, m.contributionProof.note.accepted, m.contributionProof.mine.off.tracked]) {
        expect(text).toContain("14");
      }
      // The terms' "not verified" line stays true next to the label.
      expect(m.terms.liability.salariesDisclaimer.length).toBeGreaterThan(0);
    });
  }

  it("the privacy page and the three help pages render the new lines", () => {
    const privacy = read("app/[locale]/(public)/privacy/page.tsx");
    for (const key of ["companyReviews.proof", "companySalaries.proof", "candidateExperiences.proof"]) {
      expect(privacy).toContain(`t("${key}")`);
    }
    for (const page of ["candidate-experiences", "company-salaries", "company-reviews"]) {
      expect(read(`app/[locale]/(public)/help/${page}/page.tsx`)).toContain('t("proof.body")');
    }
  });
});
