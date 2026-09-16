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
    expect(read("components/layout/NavMenu.tsx")).toContain("isActivePath(pathname, item.href)");
  });

  it("draws the active state the same way the admin tabs and the help sidebar do", () => {
    for (const file of ["components/layout/NavBar.tsx", "components/admin/AdminTabs.tsx", "components/help/HelpSidebar.tsx"]) {
      expect(read(file), file).toContain('from "@/components/layout/navLink"');
    }
  });

  it("keeps the account-free tools reachable after sign-in, in a nav-level group", () => {
    // 2026-09-14: the tools moved out of the avatar menu into a trigger in the row, so the navbar
    // loses nothing the public header offered when it takes that header's place for a signed-in
    // visitor. 2026-09-15: that trigger became "Explore", which also carries the weekly postings
    // and the company pages — one flat list, no "needs an account" split.
    const exploreMenu = read("components/layout/ExploreMenu.tsx");
    for (const href of ['"/weekly-jobs"', '"/companies"', '"/cv-tarama"', '"/benchmark"', '"/guide"']) {
      expect(exploreMenu).toContain(href);
    }
    expect(read("components/layout/NavMenu.tsx")).toContain('aria-haspopup="menu"');
    expect(navBar).toContain("<ExploreMenu />");
    expect(navBar).toContain("TOOL_LINKS.map");
    expect(read("components/layout/UserMenu.tsx")).not.toContain("TOOL_LINKS");
  });

  it("folds the row into four items and shows the two signals as icons with their counts", () => {
    // 2026-09-15 (option D3 on the header canvas): ten text items plus the paid weekly postings
    // had made the row eleven; the applications pages and the discovery pages are one group each,
    // and suggestions/notifications sit by the avatar as an inbox and a bell.
    expect(navBar).toContain('label={t("applicationsMenu")}');
    for (const href of ['"/applications"', '"/tracked-jobs"', '"/import"', '"/applications/new"']) {
      expect(navBar).toContain(href);
    }
    expect(navBar).toContain('iconLink("/suggestions"');
    expect(navBar).toContain('iconLink("/notifications"');
    expect(navBar).toContain("<ProBadge />");
  });

  it("keeps the company directory and the two contributions together inside Explore", () => {
    // 2026-09-16: a review and a salary are one click from anywhere. On main they got their own
    // "Companies" group; on the four-item row (D3) they live inside Explore instead, next to
    // the directory. Both contribute links open the same page on a different side; the mobile
    // menu lists the same three under their own heading.
    const exploreMenu = read("components/layout/ExploreMenu.tsx");
    for (const href of ['"/companies"', '"/contribute?tab=review"', '"/contribute?tab=salary"']) {
      expect(exploreMenu).toContain(href);
    }
    expect(exploreMenu).toContain("COMPANY_LINKS.map");
    expect(navBar).toContain("COMPANY_LINKS.map");
    // Not twice: the plain link left the row when the group arrived.
    expect(navBar).not.toMatch(/href: "\/companies"/);
    // The author's two lists sit together in the avatar menu and the mobile menu.
    expect(read("components/layout/UserMenu.tsx")).toContain('href="/my-salaries"');
    expect(navBar).toContain('href="/my-salaries"');
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
    for (const href of ['"/login"', '"/register"']) expect(header).toContain(href);
    expect(header).toContain("<CvScanNavButton");
    expect(header).toContain("aria-expanded");
  });

  it("hands a signed-in visitor the app's own navbar instead of a lone dashboard button", () => {
    // 2026-09-14, option D3. The "Go to dashboard" door of 2026-09-13 left a signed-in person on
    // /companies with no app menu and no avatar — it read as having been signed out.
    expect(header).toMatch(/if \(isAuthenticated\) \{\s*return <NavBar \/>;/);
    expect(header).not.toContain("goToDashboard");
  });

  it("keeps the companies pages and the account-free tools one click away", () => {
    for (const href of ['"/companies"', '"/benchmark"', '"/guide"', '"/help"']) expect(header).toContain(href);
    for (const href of ['"/companies"', '"/benchmark"', '"/cv-tarama"', '"/extension-privacy"', '"/privacy"', '"/cookies"', '"/terms"']) {
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
