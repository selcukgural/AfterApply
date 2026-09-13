import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";

/**
 * What the navigation promised on 2026-09-13, when the audit found the same state drawn three
 * ways and one destination named three ways. Source scans, like landing.contract.test.ts: there is
 * no render harness, and each rule here is one that a screenshot review would otherwise have to
 * re-check by hand.
 */
const SRC = path.join(process.cwd(), "src");
const read = (relative: string) => readFileSync(path.join(SRC, relative), "utf8");

describe("the signed-in navbar", () => {
  const navBar = read("components/layout/NavBar.tsx");

  it("marks the current section", () => {
    expect(navBar).toContain("usePathname");
    expect(navBar).toContain('aria-current={active(link.href) ? "page" : undefined}');
  });

  it("draws the active state the same way the admin tabs and the help sidebar do", () => {
    for (const file of ["components/layout/NavBar.tsx", "components/admin/AdminTabs.tsx", "components/help/HelpSidebar.tsx"]) {
      expect(read(file), file).toContain('from "@/components/layout/navLink"');
    }
  });

  it("keeps the account-free tools reachable after sign-in", () => {
    const userMenu = read("components/layout/UserMenu.tsx");
    expect(userMenu).toContain("TOOL_LINKS");
    for (const href of ['"/cv-tarama"', '"/benchmark"', '"/guide"']) {
      expect(navBar).toContain(href);
    }
  });

  it("labels its menu button from the catalogue, not a hardcoded English string", () => {
    expect(navBar).not.toContain('"Open menu"');
    expect(navBar).not.toContain('"Close menu"');
  });
});

describe("the signed-out chrome", () => {
  const header = read("components/layout/SiteHeader.tsx");
  const footer = read("components/layout/SiteFooter.tsx");

  it("is one header and one footer for the landing page and every public page", () => {
    for (const file of ["app/[locale]/page.tsx", "app/[locale]/(public)/layout.tsx"]) {
      const page = read(file);
      expect(page, file).toContain("<SiteHeader");
      expect(page, file).toContain("<SiteFooter");
    }
  });

  it("knows whether the visitor is signed in and offers the right door", () => {
    expect(header).toContain("useAuth");
    for (const href of ['"/dashboard"', '"/login"', '"/register"']) expect(header).toContain(href);
    expect(header).toContain("<CvScanNavButton");
    expect(header).toContain("aria-expanded");
  });

  it("keeps the companies pages and the account-free tools one click away", () => {
    for (const href of ['"/companies"', '"/benchmark"', '"/guide"', '"/help"']) expect(header).toContain(href);
    for (const href of ['"/companies"', '"/benchmark"', '"/cv-tarama"', '"/extension-privacy"', '"/privacy"', '"/cookies"']) {
      expect(footer).toContain(href);
    }
  });

  it("adds no traffic event and no hardcoded English menu label", () => {
    expect(header).not.toContain("trackSiteTraffic");
    expect(header).not.toContain('"Open menu"');
    expect(header).not.toContain('"Close menu"');
  });
});

describe("the help centre", () => {
  it("lists its topics from one place", () => {
    const index = read("app/[locale]/(public)/help/page.tsx");
    expect(index).toContain('from "@/lib/seo/routes"');
    expect(index).not.toMatch(/const TOPIC_LINKS = \[\s*\{/);
  });
});

describe("labels", () => {
  const trNav = tr.nav as Record<string, string>;
  const trAdmin = tr.adminTabs as Record<string, string>;
  const trHelp = tr.help.sidebar as Record<string, string>;
  const enHelp = en.help.sidebar as Record<string, string>;

  it("does not call the moderation reports what it calls the user's notifications", () => {
    expect(trAdmin.reports).not.toBe(trNav.notifications);
  });

  it("names a help topic the way its screen names itself", () => {
    expect(trHelp.trackedJobs).toBe(tr.trackedJobs.title);
    expect(trHelp.settings).toBe(tr.settings.title);
    expect(trHelp.import).toBe(tr.imports.title);
    expect(enHelp.trackedJobs).toBe(en.trackedJobs.title);
    expect(enHelp.settings).toBe(en.settings.title);
    expect(enHelp.import).toBe(en.imports.title);
  });

  it("links to the scoring page and back to the directory with one label each", () => {
    for (const file of [
      "app/[locale]/(public)/companies/page.tsx",
      "components/companyReviews/ReviewSummaryPanel.tsx",
    ]) {
      expect(read(file), file).toContain('"linkLabel"');
    }
    expect(read("app/[locale]/(public)/companies/scoring/page.tsx")).toContain('"allCompanies"');
  });

  it("explains what a review is before the directory's search box", () => {
    const page = read("app/[locale]/(public)/companies/page.tsx");
    for (const key of ['"what"', '"anonymous"', '"moderated"']) expect(page).toContain(key);
    expect(page.match(/"\/companies\/scoring"/g)).toHaveLength(1);
  });
});
