import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import { HELP_TOPICS } from "./routes";

/**
 * HELP_TOPICS is what the sidebar, the overview cards, the sitemap and the breadcrumbs share; a
 * topic listed there without a page is a 404 in the sitemap, and a page whose breadcrumb names a
 * different path tells a search engine it lives somewhere it does not.
 */
const HELP_DIR = path.join(process.cwd(), "src/app/[locale]/(public)/help");

type MessageTree = { [key: string]: string | MessageTree };
const CATALOGUES = { tr, en } as Record<string, MessageTree>;

/** Resolves a dotted key the way next-intl would, or undefined when any segment is missing. */
function message(locale: string, key: string): string | undefined {
  const value = key.split(".").reduce<string | MessageTree | undefined>(
    (node, segment) => (node && typeof node !== "string" ? node[segment] : undefined),
    CATALOGUES[locale],
  );
  return typeof value === "string" ? value : undefined;
}

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

  // The overview page builds its cards from HELP_TOPICS with template-literal keys, which the
  // static message-usage test cannot see. The weekly-jobs topic shipped with a sidebar label but
  // no overview description, so its card rendered "help.overview.topics.weeklyJobs.description".
  it.each(["tr", "en"])("each has a sidebar label and an overview card description in %s", (locale) => {
    const missing = topics.flatMap((topic) =>
      [`help.sidebar.${topic.key}`, `help.overview.topics.${topic.key}.description`].filter(
        (key) => message(locale, key) === undefined,
      ),
    );
    expect(missing).toEqual([]);
  });
});

describe("FAQ", () => {
  const page = readFileSync(path.join(HELP_DIR, "faq/page.tsx"), "utf8");
  const listed = [...page.matchAll(/"(q\d+)"/g)].map((match) => match[1]);
  const catalogued = Object.keys(tr.help.faq).filter((key) => /^q\d+$/.test(key));

  it("lists every question the catalogue has, so a new entry cannot be written and never shown", () => {
    expect(listed.sort()).toEqual(catalogued.sort());
  });

  it.each(["tr", "en"])("has a question and an answer for every listed key in %s", (locale) => {
    const missing = listed.flatMap((key) =>
      [`help.faq.${key}.question`, `help.faq.${key}.answer`].filter((path) => message(locale, path) === undefined),
    );
    expect(missing).toEqual([]);
  });
});

describe("settings help", () => {
  const page = readFileSync(path.join(HELP_DIR, "settings/page.tsx"), "utf8");

  // Same blind spot as the overview cards: StepList keys are built with template literals.
  it.each(["tr", "en"])("resolves every extension and billing step in %s", (locale) => {
    const steps = [
      ...["step1", "step2", "step3", "step4"].map((key) => `help.settings.extension.${key}`),
      ...["step1", "step2", "step3"].map((key) => `help.settings.billing.${key}`),
    ];
    const missing = steps.flatMap((prefix) =>
      [`${prefix}.title`, `${prefix}.body`].filter((key) => message(locale, key) === undefined),
    );
    expect(missing).toEqual([]);
  });

  it("covers the payments section and points at the refund policy", () => {
    expect(page).toContain('t("billing.title")');
    expect(page).toContain('href="/refund-policy"');
  });

  // The Settings page's own copy calls the section "Plan ve ödemeler" / "Plan and payments";
  // the help page has to use the same words or the reader cannot find what it describes.
  it.each(["tr", "en"])("names the billing section the way the Settings page does in %s", (locale) => {
    expect(message(locale, "help.settings.billing.title")).toBe(message(locale, "settings.billing.title"));
  });
});
