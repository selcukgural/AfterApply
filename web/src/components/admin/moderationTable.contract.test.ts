import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Source scan, like siteChrome.contract.test.ts — there is no render harness. A table cell with
 * no text colour of its own falls back to the browser's default black, which is invisible on the
 * dark theme (the "Overall" and "Open reports" columns shipped that way; reported 2026-09-14).
 * Every cell must set its colour explicitly, with a dark variant.
 */
const SRC = path.join(process.cwd(), "src");
const read = (relative: string) => readFileSync(path.join(SRC, relative), "utf8");

describe("the review moderation table", () => {
  const page = read("app/[locale]/(protected)/admin/reviews/page.tsx");
  const cells = [...page.matchAll(/<td className="([^"]*)">/g)].map((m) => m[1]);

  it("has cells to check", () => {
    expect(cells.length).toBeGreaterThan(0);
  });

  it("gives every cell an explicit text colour for both themes, or delegates to a coloured child", () => {
    for (const className of cells) {
      // The status cell is the one exception: its only content is <ReviewStatusBadge/>, which
      // paints itself.
      if (className === "px-4 py-2") {
        expect(page).toContain('<td className="px-4 py-2">\n                    <ReviewStatusBadge');
        continue;
      }
      expect(className, className).toMatch(/\btext-gray-\d{3}\b/);
      expect(className, className).toMatch(/\bdark:text-gray-\d{3}\b/);
    }
  });
});
