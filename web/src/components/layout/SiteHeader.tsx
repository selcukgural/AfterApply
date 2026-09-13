"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Link, usePathname } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { buttonClassName } from "@/components/ui/Button";
import { CvScanNavButton } from "@/components/cvScan/CvScanNavButton";
import { Logo } from "@/components/layout/Logo";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import { isActivePath, navLinkClassName } from "@/components/layout/navLink";
import type { Theme } from "@/lib/theme/theme";

export type SiteNavKey = "howItWorks" | "extension" | "features" | "companies" | "benchmark" | "guide" | "help";

export interface SiteNavLink {
  /** A route (`/companies`) or a landing anchor (`/#extension`); both go through next-intl's Link. */
  href: string;
  key: SiteNavKey;
}

/** The landing page's links: its own sections first, then the two things that work without an account. */
export const LANDING_SITE_LINKS: readonly SiteNavLink[] = [
  { href: "/#how-it-works", key: "howItWorks" },
  { href: "/#extension", key: "extension" },
  { href: "/#features", key: "features" },
  { href: "/companies", key: "companies" },
  { href: "/help", key: "help" },
];

/** Every other public page: the account-free tools, the guide and the help centre. */
export const PUBLIC_SITE_LINKS: readonly SiteNavLink[] = [
  { href: "/companies", key: "companies" },
  { href: "/benchmark", key: "benchmark" },
  { href: "/guide", key: "guide" },
  { href: "/help", key: "help" },
];

/**
 * The one header every signed-out surface shares: the landing page and every page under
 * `(public)`. Until 2026-09-13 those were two headers — the landing's, with sign-in and register,
 * and a slimmer one for the public pages with neither, a companies link that vanished on phones
 * and no menu — so a stranger landing on a guide article from a search had no way to sign up, and
 * a signed-in person on /companies had no way back to the app.
 *
 * Auth-aware on purpose: a signed-in visitor gets one button back to the dashboard, a signed-out
 * one gets sign-in and register. The app's own navbar stays where it is (the protected layout) —
 * a public page must never mount it, and it must not swap headers once the profile loads.
 */
export function SiteHeader({ initialTheme, links }: { initialTheme: Theme; links: readonly SiteNavLink[] }) {
  const t = useTranslations("siteNav");
  const { isAuthenticated } = useAuth();
  const pathname = usePathname();
  const [menuOpen, setMenuOpen] = useState(false);

  // Anchors are never "active": the landing page is one page, and underlining a section name
  // while you are anywhere on it would claim a precision the link does not have.
  const active = (href: string) => !href.startsWith("/#") && isActivePath(pathname, href);

  const authButtons = (mobile: boolean) =>
    isAuthenticated ? (
      <Link href="/dashboard" className={buttonClassName("primary", mobile ? "text-center" : "")} onClick={mobile ? () => setMenuOpen(false) : undefined}>
        {t("goToDashboard")}
      </Link>
    ) : (
      <>
        <Link
          href="/login"
          className={mobile ? buttonClassName("secondary", "text-center") : "text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100"}
          onClick={mobile ? () => setMenuOpen(false) : undefined}
        >
          {t("signIn")}
        </Link>
        <Link href="/register" className={buttonClassName("primary", mobile ? "text-center" : "")} onClick={mobile ? () => setMenuOpen(false) : undefined}>
          {t("getStarted")}
        </Link>
      </>
    );

  return (
    <header className="sticky top-0 z-40 border-b border-gray-200 bg-white/80 backdrop-blur dark:border-gray-800 dark:bg-gray-950/80">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
        <Link href="/">
          <Logo />
        </Link>

        <nav className="hidden items-center gap-6 text-sm md:flex">
          {links.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              aria-current={active(link.href) ? "page" : undefined}
              className={navLinkClassName("underline", active(link.href), "pb-0.5")}
            >
              {t(link.key)}
            </Link>
          ))}
        </nav>

        <div className="hidden items-center gap-3 md:flex">
          <LanguageSwitcher />
          <ThemeSwitcher initialTheme={initialTheme} />
          {/* Outside the signed-in branch on purpose: the scan is the one thing here that does not
              care whether you have an account, and a button that disappears once you sign in is not
              a standing entry point. It hides itself on /cv-tarama. */}
          <CvScanNavButton />
          {authButtons(false)}
        </div>

        <button
          type="button"
          onClick={() => setMenuOpen((open) => !open)}
          aria-expanded={menuOpen}
          aria-controls="site-mobile-menu"
          aria-label={menuOpen ? t("closeMenu") : t("openMenu")}
          className="flex h-9 w-9 items-center justify-center rounded-md text-gray-700 hover:bg-gray-100 md:hidden dark:text-gray-300 dark:hover:bg-gray-800"
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
        <div id="site-mobile-menu" className="border-t border-gray-200 bg-white px-4 py-4 md:hidden dark:border-gray-800 dark:bg-gray-950">
          <nav className="flex flex-col gap-1 text-sm">
            {links.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                onClick={() => setMenuOpen(false)}
                aria-current={active(link.href) ? "page" : undefined}
                className={navLinkClassName("pill", active(link.href), "px-3 py-2")}
              >
                {t(link.key)}
              </Link>
            ))}
          </nav>
          <div className="mt-4 flex items-center gap-3 px-3">
            <LanguageSwitcher />
            <ThemeSwitcher initialTheme={initialTheme} />
          </div>
          <div className="mt-4 flex flex-col gap-2">
            {/* Above the auth buttons, so the reading order is scan → sign in → register: least
                asked of the visitor first. */}
            <CvScanNavButton className="block text-center" onNavigate={() => setMenuOpen(false)} />
            {authButtons(true)}
          </div>
        </div>
      )}
    </header>
  );
}
