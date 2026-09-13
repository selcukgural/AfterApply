"use client";

import { useEffect, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { ADMIN_NAV_HREF } from "@/lib/auth/adminNav";
import { TOOL_LINKS } from "@/components/layout/NavBar";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import type { Theme } from "@/lib/theme/theme";

interface UserMenuProps {
  name: string;
  initials: string;
  onLogout: () => void;
  initialTheme: Theme;
  /** Renders the admin entry. Decided by the caller from the profile — see canSeeAdminNav. */
  showAdmin: boolean;
}

// Consolidates the navbar's utility items (help, account settings, language,
// theme, logout) behind one fixed-width trigger instead of listing them
// inline, so it can never push the primary nav onto a second line no matter
// how long a locale's labels get.
export function UserMenu({ name, initials, onLogout, initialTheme, showAdmin }: UserMenuProps) {
  const t = useTranslations("nav");
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    const handlePointerDown = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };

    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-haspopup="menu"
        className="flex items-center gap-2 rounded-md py-1.5 pl-1.5 pr-2 text-sm text-gray-600 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800"
      >
        <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-gray-200 text-xs font-semibold text-gray-700 dark:bg-gray-700 dark:text-gray-200">
          {initials}
        </span>
        <span className="max-w-[9rem] truncate">{name}</span>
        <svg viewBox="0 0 24 24" className="h-4 w-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
        </svg>
      </button>

      {open && (
        <div
          role="menu"
          className="absolute right-0 z-50 mt-2 w-60 rounded-md border border-gray-200 bg-white py-1 shadow-lg dark:border-gray-800 dark:bg-gray-900"
        >
          <Link
            role="menuitem"
            href="/help"
            onClick={() => setOpen(false)}
            className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
          >
            {t("help")}
          </Link>
          <Link
            role="menuitem"
            href="/settings"
            onClick={() => setOpen(false)}
            className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
          >
            {t("accountSettings")}
          </Link>
          <Link
            role="menuitem"
            href="/my-reviews"
            onClick={() => setOpen(false)}
            className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
          >
            {t("myReviews")}
          </Link>
          {/* Below the two everyone has, and separated from them: it is the same menu the rest of
              the utility links live in, so an admin reaches the page by clicking rather than by
              remembering a URL, without the primary nav growing an item that only one account
              would ever see. */}
          {showAdmin && (
            <Link
              role="menuitem"
              href={ADMIN_NAV_HREF}
              onClick={() => setOpen(false)}
              className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
            >
              {t("admin")}
            </Link>
          )}

          <div className="my-1 border-t border-gray-100 dark:border-gray-800" />

          {/* The account-free tools. They are in the landing navbar and footer for strangers;
              without this group a signed-in person could reach them only by signing out. */}
          <p className="px-4 pt-2 pb-1 text-xs font-semibold tracking-wide text-gray-500 uppercase dark:text-gray-400">{t("tools")}</p>
          {TOOL_LINKS.map((link) => (
            <Link
              key={link.href}
              role="menuitem"
              href={link.href}
              onClick={() => setOpen(false)}
              className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
            >
              {t(link.key)}
            </Link>
          ))}

          <div className="my-1 border-t border-gray-100 dark:border-gray-800" />

          <div className="flex items-center justify-between px-4 py-2">
            <LanguageSwitcher />
            <ThemeSwitcher initialTheme={initialTheme} />
          </div>

          <div className="my-1 border-t border-gray-100 dark:border-gray-800" />

          <button
            role="menuitem"
            type="button"
            onClick={onLogout}
            className="block w-full px-4 py-2 text-left text-sm text-red-600 hover:bg-gray-50 dark:text-red-400 dark:hover:bg-gray-800"
          >
            {t("logout")}
          </button>
        </div>
      )}
    </div>
  );
}
