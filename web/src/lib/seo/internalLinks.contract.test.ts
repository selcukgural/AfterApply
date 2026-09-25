import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";

const root = join(__dirname, "../..");

function walk(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry);
    return statSync(path).isDirectory() ? walk(path) : [path];
  });
}

/**
 * Every URL on the site is /tr/… or /en/… (`localePrefix: "always"`). A plain <a> to an in-site
 * path skips the prefix the i18n `Link` adds, so the page ships "/login?next=…": one more redirect
 * per click, a redirecting link in every crawl, and the language left to a cookie. The sign-in
 * links on the company pages were exactly that until 2026-09-25.
 *
 * A plain <a> stays right for mailto:, #anchors, external sites and file downloads — none of which
 * starts with "/" or is a sign-in address.
 */
describe("in-site links", () => {
  const sources = walk(root)
    .filter((path) => /\.tsx$/.test(path) && !/\.test\.tsx$/.test(path))
    .map((path) => ({ path: relative(root, path), text: readFileSync(path, "utf8") }));

  it("go through the locale-aware Link, not a plain <a>", () => {
    const offenders = sources.flatMap(({ path, text }) =>
      [...text.matchAll(/<a\s[^>]*href=(?:"\/|\{`\/|\{"\/|\{signInHref\})/g)].map((match) => `${path}: ${match[0]}`),
    );
    expect(offenders).toEqual([]);
  });
});
