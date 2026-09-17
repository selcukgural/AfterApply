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
    expect(navBar).toContain('aria-current={active(entry.href) ? "page" : undefined}');
    expect(navBar).toContain('aria-current={isNavItemActive(pathname, item.href) ? "page" : undefined}');
    expect(read("components/layout/NavMenu.tsx")).toContain("isNavItemActive(pathname, item.href)");
  });

  it("draws the active state the same way the admin tabs and the help sidebar do", () => {
    for (const file of ["components/layout/NavBar.tsx", "components/admin/AdminTabs.tsx", "components/help/HelpSidebar.tsx"]) {
      expect(read(file), file).toContain('from "@/components/layout/navLink"');
    }
  });

  it("draws the row and the mobile drawer from one list, so they cannot drift apart", () => {
    // 2026-09-17 (variant A on the navigation canvas): the drawer had grown its own grouping —
    // no "Explore", two headings the row never showed, no "New application". Both surfaces now
    // map over buildNavEntries; nothing here is allowed to hand-list a section again.
    expect(navBar).toContain("buildNavEntries(config)");
    expect(navBar.match(/entries\.map\(/g)).toHaveLength(2);
    expect(navBar).not.toContain("ExploreMenu");
    expect(navBar).not.toContain("COMPANY_LINKS");
    expect(navBar).not.toContain("TOOL_LINKS");
    expect(navBar).not.toMatch(/href: "\/companies"/);
  });

  it("shows the two signals as icons with their counts and the primary action as a button", () => {
    expect(navBar).toContain('iconLink("/suggestions"');
    expect(navBar).toContain('iconLink("/notifications"');
    expect(navBar).toContain('href="/applications/new"');
    expect(navBar).toContain("<ProBadge />");
    expect(read("components/layout/NavMenu.tsx")).toContain('aria-haspopup="menu"');
  });

  it("keeps content out of the avatar menu and orders it profile → settings → help → sign out", () => {
    // What a person wrote lives next to what it is about (the Companies group); the avatar menu
    // is for the account. Help used to be its first item. Since 2026-09-17 the profile page
    // (who the account is) comes before the settings page (how it is wired).
    const userMenu = read("components/layout/UserMenu.tsx");
    expect(userMenu).not.toContain("/my-reviews");
    expect(userMenu).not.toContain("/my-salaries");
    expect(userMenu).not.toContain("TOOL_LINKS");
    expect(userMenu.indexOf('href="/profile"')).toBeLessThan(userMenu.indexOf('href="/settings"'));
    expect(userMenu.indexOf('href="/settings"')).toBeLessThan(userMenu.indexOf('href="/help"'));
    expect(userMenu.indexOf('href="/help"')).toBeLessThan(userMenu.indexOf("onClick={onLogout}"));
    // The drawer's account block keeps the same order.
    expect(navBar.indexOf('href="/profile"')).toBeLessThan(navBar.indexOf('href="/settings"'));
    expect(navBar.indexOf('href="/settings"')).toBeLessThan(navBar.indexOf('href="/help"'));
    expect(navBar).not.toContain('href="/my-reviews"');
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
      expect(page, file).toContain("<SiteHeader />");
      expect(page, file).toContain("<SiteFooter");
    }
  });

  it("shows the same links on every signed-out page", () => {
    // 2026-09-17: the landing used to have its own set and the other public pages another, so
    // the menu changed under a visitor who went from the home page to Companies. One list,
    // no `links` prop, and the only anchor left is "how it works".
    expect(header).toContain("export const SITE_LINKS");
    expect(header).not.toContain("LANDING_SITE_LINKS");
    expect(header).not.toContain("PUBLIC_SITE_LINKS");
    expect(header).not.toContain("links: readonly SiteNavLink[]");
    expect(header.match(/href: "\/#/g)).toHaveLength(1);
    expect(read("app/[locale]/not-found.tsx")).toContain("<SiteHeader />");
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

  it("keeps the legal texts in their own footer column, apart from the pages to browse", () => {
    // 2026-09-17: six legal links used to close a ten-line "Resources" list that opened with the
    // tools and the guide.
    for (const list of ["PRODUCT_LINKS", "EXPLORE_LINKS", "LEGAL_LINKS"]) expect(footer).toContain(list);
    expect(footer).not.toContain("RESOURCE_LINKS");
    expect(footer).toContain("CHROME_WEB_STORE_URL");
    expect(footer.indexOf('"/guide"')).toBeLessThan(footer.indexOf("LEGAL_LINKS"));
  });

  it("lists the Pro plan's sale terms in the footer regardless of the payments flag", () => {
    // 2026-09-16: the distance-sales agreement and the refund policy went final. A consumer (and
    // PayTR's merchant review) must find them without first reaching the checkout, so they are
    // plain footer links, not gated on payments.enabled.
    expect(footer).toContain('"/terms-of-sale"');
    expect(footer).toContain('"/refund-policy"');
    expect(footer).not.toContain("payments?.enabled");
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
