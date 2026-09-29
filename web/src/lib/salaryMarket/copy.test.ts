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

    // One source since 2026-09-27, so nothing is pooled any more and the pages no longer say so.
    it(`explains the ranges, the currency filter and that amounts are nominal (${language})`, () => {
      expect(Object.keys(sources.method).sort()).toEqual(["band", "currency", "nominal"]);
    });

    // The list page's short source line (canvas variant A, 2026-09-29) keeps every promise of the
    // long box on the detail page: the source and its link, the permission, the threshold and the
    // three method points.
    it(`keeps the list page's short source line as complete as the long box (${language})`, () => {
      expect(sources.strip.source).toContain("<link>{name}</link>");
      expect(sources.strip.source).toContain("{first}");
      expect(Object.keys(sources.strip.facts).sort()).toEqual(["band", "currency", "nominal", "threshold"]);
      expect(sources.strip.facts.threshold.title).toContain("{minimum}");
      expect(sources.strip.facts.band.text).toContain("≥");
    });

    // The list page's lead names the survey and links to it (2026-09-29).
    it(`names and links the survey in the list page's lead (${language})`, () => {
      expect(m.salaryMarket.list.lead).toContain("<link>{name}</link>");
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
