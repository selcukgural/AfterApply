"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { buttonClassName } from "@/components/ui/Button";
import { Logo } from "@/components/layout/Logo";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import type { Theme } from "@/lib/theme/theme";

const ANCHOR_LINKS = [
  { href: "#how-it-works", key: "howItWorks" },
  { href: "#features", key: "features" },
  { href: "#mission", key: "mission" },
] as const;

export function LandingNavbar({ initialTheme }: { initialTheme: Theme }) {
  const t = useTranslations("landing.navbar");
  const { isAuthenticated } = useAuth();
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <header className="sticky top-0 z-40 border-b border-gray-200 bg-white/80 backdrop-blur dark:border-gray-800 dark:bg-gray-950/80">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
        <Link href="/">
          <Logo />
        </Link>

        <nav className="hidden items-center gap-6 text-sm text-gray-600 md:flex dark:text-gray-400">
          {ANCHOR_LINKS.map((link) => (
            <a key={link.href} href={link.href} className="hover:text-gray-900 dark:hover:text-gray-100">
              {t(link.key)}
            </a>
          ))}
        </nav>

        <div className="hidden items-center gap-3 md:flex">
          <LanguageSwitcher />
          <ThemeSwitcher initialTheme={initialTheme} />
          {isAuthenticated ? (
            <Link href="/dashboard" className={buttonClassName("primary")}>
              {t("goToDashboard")}
            </Link>
          ) : (
            <>
              <Link href="/login" className="text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100">
                {t("signIn")}
              </Link>
              <Link href="/register" className={buttonClassName("primary")}>
                {t("getStarted")}
              </Link>
            </>
          )}
        </div>

        <button
          type="button"
          onClick={() => setMenuOpen((open) => !open)}
          aria-expanded={menuOpen}
          aria-controls="landing-mobile-menu"
          aria-label={menuOpen ? "Close menu" : "Open menu"}
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
        <div
          id="landing-mobile-menu"
          className="border-t border-gray-200 bg-white px-4 py-4 md:hidden dark:border-gray-800 dark:bg-gray-950"
        >
          <nav className="flex flex-col gap-3 text-sm text-gray-600 dark:text-gray-400">
            {ANCHOR_LINKS.map((link) => (
              <a key={link.href} href={link.href} onClick={() => setMenuOpen(false)} className="hover:text-gray-900 dark:hover:text-gray-100">
                {t(link.key)}
              </a>
            ))}
          </nav>
          <div className="mt-4 flex items-center gap-3">
            <LanguageSwitcher />
            <ThemeSwitcher initialTheme={initialTheme} />
          </div>
          <div className="mt-4 flex flex-col gap-2">
            {isAuthenticated ? (
              <Link href="/dashboard" className={buttonClassName("primary", "text-center")} onClick={() => setMenuOpen(false)}>
                {t("goToDashboard")}
              </Link>
            ) : (
              <>
                <Link href="/login" className={buttonClassName("secondary", "text-center")} onClick={() => setMenuOpen(false)}>
                  {t("signIn")}
                </Link>
                <Link href="/register" className={buttonClassName("primary", "text-center")} onClick={() => setMenuOpen(false)}>
                  {t("getStarted")}
                </Link>
              </>
            )}
          </div>
        </div>
      )}
    </header>
  );
}
