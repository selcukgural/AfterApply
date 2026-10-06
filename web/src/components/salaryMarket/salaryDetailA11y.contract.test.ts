import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * The occupation page's three axe findings (2026-10-06), pinned as source scans because this suite
 * runs in node with no DOM: each year's chart target is a labelled SVG shape, the year-change chips
 * keep readable text on their gray chip, and the wide years table can be scrolled from the keyboard.
 */
const read = (file: string) => readFileSync(path.join(process.cwd(), "src/components/salaryMarket", file), "utf8");

describe("the occupation page", () => {
  it("gives each focusable year target in the chart a role for its label", () => {
    const chart = read("SalaryTrendChart.tsx");
    const target = chart.slice(chart.indexOf("<rect\n"), chart.indexOf("/>", chart.indexOf("<rect\n")));
    expect(target).toContain("tabIndex={0}");
    expect(target).toContain('role="img"');
    expect(target).toContain("aria-label=");
  });

  it("keeps the year-change chip text darker than gray-500 on its gray-100 chip", () => {
    const chart = read("SalaryTrendChart.tsx");
    const chip = chart.slice(chart.indexOf("bg-gray-100 px-2.5 py-1.5"), chart.indexOf("</li>", chart.indexOf("bg-gray-100 px-2.5 py-1.5")));
    expect(chip).not.toMatch(/(^|\s)text-gray-500(\s|")/);
  });

  it("makes the scrolling years table a named, focusable region", () => {
    const sections = read("SalarySections.tsx");
    const table = sections.slice(sections.indexOf("export async function SalaryYearsTable"), sections.indexOf("<table"));
    expect(table).toContain('role="region"');
    expect(table).toContain('aria-label={t("caption")}');
    expect(table).toContain("tabIndex={0}");
  });
});
