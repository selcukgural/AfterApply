import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * What the public directory card promises. A source scan, like the landing page's own contract
 * suite: the rules here are about what the card does and does not print, which a render test
 * would check more slowly and no more reliably.
 */
const read = (relative: string) => readFileSync(path.join(process.cwd(), relative), "utf8");
const source = read("src/components/companyReviews/CompanyDirectory.tsx");
const stripComments = (input: string) => input.replace(/\{\/\*[\s\S]*?\*\/\}/g, "").replace(/\/\*[\s\S]*?\*\//g, "");

describe("the directory card", () => {
  /**
   * 2026-09-22 (research report item 0.3). A score needs three approved reviews, so eighteen of
   * the nineteen companies on the live directory printed "Henüz puan yok" where the stars go —
   * a page of absences, above count lines that each named a real contribution. The absence is
   * gone; the stars still appear the moment there is a score.
   */
  it("prints the stars only when there is a score, and no 'no score yet' line", () => {
    const markup = stripComments(source);
    expect(markup).toContain("{company.score !== null && (");
    expect(markup).toContain("<StarRating");
    expect(markup).not.toContain("noScore");

    for (const name of ["tr", "en"]) {
      const messages = JSON.parse(read(`messages/${name}.json`)) as { companies: { directory: Record<string, unknown> } };
      expect(messages.companies.directory, name).not.toHaveProperty("noScore");
    }
  });

  /** The card still says what it holds: the count lines are the reason the score line could go. */
  it("keeps the per-kind count lines under the name", () => {
    expect(source).toContain("directoryCountLines(company)");
    expect(source).toContain("COUNT_DOT[line.key]");
  });
});
