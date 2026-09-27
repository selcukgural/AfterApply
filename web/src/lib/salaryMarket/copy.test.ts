import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";

/** What the salary pages must always say about their numbers (DECISIONS.md 2026-09-27). */
describe("salary pages' copy", () => {
  for (const [language, m] of [["tr", tr], ["en", en]] as const) {
    const sources = m.salaryMarket.sources;

    it(`names the source, the permission and the threshold (${language})`, () => {
      expect(sources.source).toContain("{name}");
      expect(sources.source).toContain("<link>");
      expect(sources.permission.length).toBeGreaterThan(0);
      expect(sources.threshold).toContain("{minimum}");
    });

    it(`explains the ranges, the pooling, the currency filter and that amounts are nominal (${language})`, () => {
      expect(Object.keys(sources.method).sort()).toEqual(["band", "currency", "nominal", "pooled"]);
    });

    it(`says why the extremes are not shown (${language})`, () => {
      expect(m.salaryMarket.chart.extremesNote.length).toBeGreaterThan(0);
      expect(m.salaryMarket.chart.nominalNote.length).toBeGreaterThan(0);
    });

    it(`keeps the site's tone: no exclamation marks (${language})`, () => {
      expect(JSON.stringify(m.salaryMarket)).not.toContain("!");
    });
  }
});
