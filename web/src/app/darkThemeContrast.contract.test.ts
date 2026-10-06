import { readdirSync, readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Dark-theme text contrast (DECISIONS 2026-10-06). The dark accent was #3d7ce0, which left white
 * button text at 4.07:1, and tertiary text used gray-500, 3.67:1 on a dark card. Both now clear
 * WCAG AA's 4.5:1; these tests read the values out of globals.css so a later tweak that slides
 * back under the line fails here rather than in the next PageSpeed run.
 */
const css = readFileSync(path.join(process.cwd(), "src/app/globals.css"), "utf8");

// Tailwind's own dark surfaces as axe resolves them: gray-950 page, gray-900 card, gray-800 chip.
const DARK_PAGE = "#030712";
const DARK_CARD = "#101828";
const DARK_CHIP = "#1e2939";

function luminance(hex: string): number {
  const channels = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255);
  const [r, g, b] = channels.map((c) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrast(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

function token(block: string, name: string): string {
  const match = block.match(new RegExp(`--${name}:\\s*(#[0-9a-fA-F]{6})`));
  expect(match, `--${name} is a six-digit hex`).not.toBeNull();
  return match![1];
}

const darkBlock = css.slice(css.indexOf(".dark {"), css.indexOf("}", css.indexOf(".dark {")));

describe("dark theme", () => {
  it("keeps white button text readable on the accent and on its hover", () => {
    expect(contrast("#ffffff", token(darkBlock, "accent"))).toBeGreaterThanOrEqual(4.5);
    expect(contrast("#ffffff", token(darkBlock, "accent-strong"))).toBeGreaterThanOrEqual(4.5);
  });

  it("keeps gray-450 readable on the page, a card and a chip", () => {
    const gray450 = token(css, "color-gray-450");
    for (const surface of [DARK_PAGE, DARK_CARD, DARK_CHIP]) {
      expect(contrast(gray450, surface), `gray-450 on ${surface}`).toBeGreaterThanOrEqual(4.5);
    }
  });

  it("uses gray-450, not gray-500, for dark-mode text", () => {
    const offenders: string[] = [];
    const walk = (dir: string) => {
      for (const entry of readdirSync(dir, { withFileTypes: true })) {
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) walk(full);
        else if (/\.tsx?$/.test(entry.name) && !entry.name.includes(".test.") && readFileSync(full, "utf8").includes("dark:text-gray-500")) {
          offenders.push(path.relative(process.cwd(), full));
        }
      }
    };
    walk(path.join(process.cwd(), "src"));
    expect(offenders).toEqual([]);
  });
});
