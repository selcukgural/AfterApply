import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { HELP_TOPICS } from "./routes";

/**
 * HELP_TOPICS is what the sidebar, the overview cards, the sitemap and the breadcrumbs share; a
 * topic listed there without a page is a 404 in the sitemap, and a page whose breadcrumb names a
 * different path tells a search engine it lives somewhere it does not.
 */
const HELP_DIR = path.join(process.cwd(), "src/app/[locale]/(public)/help");

describe("help topics", () => {
  const topics = HELP_TOPICS.filter((topic) => topic.href !== "/help");

  it.each(topics.map((topic) => topic.href))("%s has a page and a breadcrumb naming the same path", (href) => {
    const page = path.join(HELP_DIR, href.replace("/help/", ""), "page.tsx");
    expect(existsSync(page), page).toBe(true);
    expect(readFileSync(page, "utf8")).toContain(`<HelpBreadcrumbJsonLd path="${href}" />`);
  });

  it("uses slugs the API's visit counter accepts", () => {
    for (const topic of topics) {
      expect(topic.href.replace("/help/", "")).toMatch(/^[a-z0-9][a-z0-9-]{0,63}$/);
    }
  });
});
