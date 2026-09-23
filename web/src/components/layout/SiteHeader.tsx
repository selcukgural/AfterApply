"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link, usePathname } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { useClientConfig } from "@/hooks/useClientConfig";
import { buttonClassName } from "@/components/ui/Button";
import { Logo } from "@/components/layout/Logo";
import { PreferencesControls, PreferencesMenu } from "@/components/layout/PreferencesMenu";
import { NavBar } from "@/components/layout/NavBar";
import { isActivePath, navLinkClassName } from "@/components/layout/navLink";
import { NavMenu } from "@/components/layout/NavMenu";
import { LandingIcon, type LandingIcon as LandingIconName } from "@/components/landing/landingIcons";
import { CV_SCAN_PATHS } from "@/lib/cvScan/path";
import { OFFER_COMPARE_PATHS } from "@/lib/offerCompare/path";
import { pathFor, type LocalisedPath } from "@/lib/seo/routes";

export type SiteNavKey = "howItWorks" | "companies" | "tools" | "guide" | "blog" | "help";

export interface SiteNavLink {
  /** A route (`/companies`) or a landing anchor (`/#how-it-works`); both go through next-intl's Link.
   *  Empty for the tools menu, which is a trigger rather than a destination. */
  href: string;
  key: SiteNavKey;
}

export type SiteToolKey = "responseRates" | "benchmark" | "offerCompare" | "cvScan";

export interface SiteTool {
  href: LocalisedPath;
  key: SiteToolKey;
  icon: LandingIconName;
}

/**
 * What the "Tools" menu holds (header canvas H1 and "Üst menü · son hâli", 2026-09-24): the pages
 * that work without an account, each with a line saying what it does. It took the place of the
 * lone "Benchmark" link when the offer comparison arrived, and then of the response-rate link and
 * the separate "Scan your CV" button, when a measurement showed the row did not fit at any width
 * (items wrapping onto two lines from 1024px up, the page scrolling sideways below that). The
 * signed-in navbar already kept response rates among its tools; the two menus now agree.
 */
export const SITE_TOOLS: readonly SiteTool[] = [
  { href: "/benchmark", key: "benchmark", icon: "analytics" },
  { href: OFFER_COMPARE_PATHS, key: "offerCompare", icon: "offer" },
  { href: CV_SCAN_PATHS, key: "cvScan", icon: "cv" },
];

/** The public sector response-rate table (2026-09-22), first in the menu when its server flag is on. */
const RESPONSE_RATES_TOOL: SiteTool = { href: "/response-rates", key: "responseRates", icon: "analytics" };

export function siteToolsFor(hasResponseRates: boolean): readonly SiteTool[] {
  return hasResponseRates ? [RESPONSE_RATES_TOOL, ...SITE_TOOLS] : SITE_TOOLS;
}

/**
 * The one set of links for every signed-out page — the landing, the public pages and the 404.
 * Until 2026-09-17 the landing had its own set (three of its section anchors, companies, help)
 * and every other public page another (companies, benchmark, guide, help), so a visitor who
 * went from the home page to Companies watched the menu change under them. "How it works" is
 * the one anchor kept: it resolves from any page and is what a marketing header is expected to
 * open with; the extension and features anchors are the footer's "Product" column, and the
 * about page (the old mission section's home) is in its "Explore" column.
 */
export const SITE_LINKS: readonly SiteNavLink[] = [
  { href: "/#how-it-works", key: "howItWorks" },
  { href: "/companies", key: "companies" },
  { href: "", key: "tools" },
  { href: "/guide", key: "guide" },
  { href: "/help", key: "help" },
];

/** The blog's link, between the guide and the help centre — but only once there is a published
 *  post to read (`blog.hasPublishedPosts`, DECISIONS.md 2026-09-19). A "Blog" that opens on nothing
 *  is worse than no blog, so the header follows the server rather than the route existing. */
const BLOG_LINK: SiteNavLink = { href: "/blog", key: "blog" };

export function siteLinksFor(hasBlog: boolean): readonly SiteNavLink[] {
  let result: readonly SiteNavLink[] = SITE_LINKS;
  if (hasBlog) {
    const helpIndex = result.findIndex((link) => link.key === "help");
    result = [...result.slice(0, helpIndex), BLOG_LINK, ...result.slice(helpIndex)];
  }
  return result;
}

/**
 * The one header every signed-out surface shares: the landing page and every page under
 * `(public)`. Until 2026-09-13 those were two headers — the landing's, with sign-in and register,
 * and a slimmer one for the public pages with neither, a companies link that vanished on phones
 * and no menu — so a stranger landing on a guide article from a search had no way to sign up, and
 * a signed-in person on /companies had no way back to the app.
 *
 * Auth-aware: a signed-out visitor gets this header with sign-in and register. **A signed-in one
 * gets the app's own NavBar instead** (2026-09-14, option D3 on the header canvas — reversing the
 * 2026-09-13 choice of a "Go to dashboard" button). That button left a signed-in person who
 * clicked "Companies" in the app on a page with no app menu and no avatar, which read as having
 * been signed out. The three costs named when this was first rejected are accepted knowingly: the
 * header swaps once `authStore.hydrate()` runs after mount (one frame, the same moment the old
 * "Go to dashboard" button appeared); the suggestion/notification counters are fetched on public
 * pages too; and the public links (Benchmark, Guide, CV scan) live in the NavBar's Tools group
 * rather than in its row.
 *
 * Width (2026-09-24): the full row shows from `lg` (1024px) and every item is `whitespace-nowrap`,
 * so a row that ever grows too long again overflows visibly in a review rather than folding its
 * labels onto two lines. Below `lg` the phone menu takes over.
 */
export function SiteHeader() {
  const t = useTranslations("siteNav");
  const locale = useLocale();
  const { isAuthenticated } = useAuth();
  const { config } = useClientConfig();
  const pathname = usePathname();
  const [menuOpen, setMenuOpen] = useState(false);

  if (isAuthenticated) {
    return <NavBar />;
  }

  const links = siteLinksFor(config.blog?.enabled === true && config.blog.hasPublishedPosts === true);
  const tools = siteToolsFor(config.responseRates?.enabled === true);

  const toolItems = tools.map((tool) => ({
    href: pathFor(tool.href, locale),
    label: (
      <span className="flex items-start gap-3 py-0.5">
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-accent-wash text-accent-ink">
          <LandingIcon name={tool.icon} className="h-[18px] w-[18px]" />
        </span>
        <span className="flex flex-col gap-0.5">
          <span className="font-semibold text-gray-900 dark:text-gray-100">{t(`toolsMenu.${tool.key}.title`)}</span>
          <span className="text-[13px] leading-snug font-normal text-gray-600 dark:text-gray-400">
            {t(`toolsMenu.${tool.key}.description`)}
          </span>
        </span>
      </span>
    ),
  }));

  // Anchors are never "active": the landing page is one page, and underlining a section name
  // while you are anywhere on it would claim a precision the link does not have.
  const active = (href: string) => !href.startsWith("/#") && isActivePath(pathname, href);

  const authButtons = (mobile: boolean) => (
    <>
      <Link
        href="/login"
        className={mobile ? buttonClassName("secondary", "text-center") : "text-sm whitespace-nowrap text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100"}
        onClick={mobile ? () => setMenuOpen(false) : undefined}
      >
        {t("signIn")}
      </Link>
      <Link href="/register" className={buttonClassName("primary", mobile ? "text-center" : "whitespace-nowrap")} onClick={mobile ? () => setMenuOpen(false) : undefined}>
        {t("getStarted")}
      </Link>
    </>
  );

  return (
    <header className="sticky top-0 z-40 border-b border-gray-200 bg-white/80 backdrop-blur dark:border-gray-800 dark:bg-gray-950/80">
      <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-3">
        <Link href="/" className="shrink-0">
          <Logo />
        </Link>

        <nav className="hidden items-center gap-6 text-sm lg:flex">
          {links.map((link) =>
            link.key === "tools" ? (
              <NavMenu key="tools" label={t("tools")} items={toolItems} menuClassName="w-80 p-1.5" />
            ) : (
            <Link
              key={link.href}
              href={link.href}
              aria-current={active(link.href) ? "page" : undefined}
              className={navLinkClassName("underline", active(link.href), "pb-0.5 whitespace-nowrap")}
            >
              {t(link.key)}
            </Link>
            ),
          )}
        </nav>

        <div className="hidden shrink-0 items-center gap-3 lg:flex">
          <PreferencesMenu />
          {authButtons(false)}
        </div>

        <button
          type="button"
          onClick={() => setMenuOpen((open) => !open)}
          aria-expanded={menuOpen}
          aria-controls="site-mobile-menu"
          aria-label={menuOpen ? t("closeMenu") : t("openMenu")}
          className="flex h-9 w-9 items-center justify-center rounded-md text-gray-700 hover:bg-gray-100 lg:hidden dark:text-gray-300 dark:hover:bg-gray-800"
        >
          {menuOpen ? (
            <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          ) : (
            <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
              <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5M3.75 17.25h16.5" />
            </svg>
          )}
        </button>
      </div>

      {menuOpen && (
        <div id="site-mobile-menu" className="border-t border-gray-200 bg-white px-4 py-4 lg:hidden dark:border-gray-800 dark:bg-gray-950">
          <nav className="flex flex-col gap-1 text-sm">
            {links.map((link) =>
              link.key === "tools" ? (
                // The phone menu is already a list: the tools are a labelled run of it, not a
                // menu inside a menu.
                <div key="tools" role="group" aria-labelledby="site-mobile-tools" className="flex flex-col gap-1">
                  <span id="site-mobile-tools" className="px-3 pt-2 text-xs font-medium tracking-wide text-gray-500 uppercase dark:text-gray-400">
                    {t("tools")}
                  </span>
                  {tools.map((tool) => {
                    const href = pathFor(tool.href, locale);
                    return (
                      <Link
                        key={tool.key}
                        href={href}
                        onClick={() => setMenuOpen(false)}
                        aria-current={active(href) ? "page" : undefined}
                        className={navLinkClassName("pill", active(href), "px-3 py-2")}
                      >
                        {t(`toolsMenu.${tool.key}.title`)}
                      </Link>
                    );
                  })}
                </div>
              ) : (
              <Link
                key={link.href}
                href={link.href}
                onClick={() => setMenuOpen(false)}
                aria-current={active(link.href) ? "page" : undefined}
                className={navLinkClassName("pill", active(link.href), "px-3 py-2")}
              >
                {t(link.key)}
              </Link>
              ),
            )}
          </nav>
          <div className="mt-4 rounded-lg bg-gray-50 p-3 dark:bg-gray-900">
            <PreferencesControls inline />
          </div>
          <div className="mt-4 flex flex-col gap-2">{authButtons(true)}</div>
        </div>
      )}
    </header>
  );
}
