"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { useSuggestionCount } from "@/hooks/useSuggestionCount";
import { useNotificationCount } from "@/hooks/useNotificationCount";
import { Button } from "@/components/ui/Button";
import { Logo } from "@/components/layout/Logo";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import { UserMenu } from "@/components/layout/UserMenu";
import type { Theme } from "@/lib/theme/theme";

const NAV_LINKS = [
  { href: "/dashboard", key: "dashboard" },
  { href: "/applications", key: "applications" },
  { href: "/tracked-jobs", key: "trackedJobs" },
  { href: "/import", key: "import" },
] as const;

export function NavBar({ initialTheme }: { initialTheme: Theme }) {
  const { user, logout } = useAuth();
  const router = useRouter();
  const t = useTranslations("nav");
  const { data: suggestionCount } = useSuggestionCount();
  const { data: notificationCount } = useNotificationCount();
  const [menuOpen, setMenuOpen] = useState(false);

  const handleLogout = async () => {
    await logout();
    router.replace("/login");
  };

  const fullName = user ? `${user.firstName} ${user.lastName}` : "";
  const initials = user ? `${user.firstName[0] ?? ""}${user.lastName[0] ?? ""}`.toUpperCase() : "";

  const suggestionsLink = (onNavigate?: () => void) => (
    <Link
      href="/suggestions"
      onClick={onNavigate}
      className="flex items-center gap-1.5 whitespace-nowrap hover:text-gray-900 dark:hover:text-gray-100"
    >
      {t("suggestions")}
      {!!suggestionCount && (
        <span className="inline-flex min-w-[1.25rem] items-center justify-center rounded-full bg-blue-600 px-1.5 py-0.5 text-xs font-semibold leading-none text-white">
          {suggestionCount}
        </span>
      )}
    </Link>
  );

  const notificationsLink = (onNavigate?: () => void) => (
    <Link
      href="/notifications"
      onClick={onNavigate}
      className="flex items-center gap-1.5 whitespace-nowrap hover:text-gray-900 dark:hover:text-gray-100"
    >
      {t("notifications")}
      {!!notificationCount && (
        <span className="inline-flex min-w-[1.25rem] items-center justify-center rounded-full bg-blue-600 px-1.5 py-0.5 text-xs font-semibold leading-none text-white">
          {notificationCount}
        </span>
      )}
    </Link>
  );

  return (
    <header className="border-b border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
      <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
        <div className="flex items-center gap-6">
          <Link href="/dashboard">
            <Logo />
          </Link>
          <nav className="hidden items-center gap-4 text-sm text-gray-600 md:flex dark:text-gray-400">
            {NAV_LINKS.map((link) => (
              <Link key={link.href} href={link.href} className="whitespace-nowrap hover:text-gray-900 dark:hover:text-gray-100">
                {t(link.key)}
              </Link>
            ))}
            {suggestionsLink()}
            {notificationsLink()}
          </nav>
        </div>

        <div className="hidden md:block">
          {user && <UserMenu name={fullName} initials={initials} onLogout={handleLogout} initialTheme={initialTheme} />}
        </div>

        <button
          type="button"
          onClick={() => setMenuOpen((open) => !open)}
          aria-expanded={menuOpen}
          aria-controls="app-mobile-menu"
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
        <div id="app-mobile-menu" className="border-t border-gray-200 px-4 py-4 md:hidden dark:border-gray-800">
          <nav className="flex flex-col gap-3 text-sm text-gray-600 dark:text-gray-400">
            {NAV_LINKS.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                onClick={() => setMenuOpen(false)}
                className="hover:text-gray-900 dark:hover:text-gray-100"
              >
                {t(link.key)}
              </Link>
            ))}
            {suggestionsLink(() => setMenuOpen(false))}
            {notificationsLink(() => setMenuOpen(false))}
          </nav>

          <div className="mt-4 border-t border-gray-100 pt-4 dark:border-gray-800">
            {user && <p className="mb-3 text-sm font-medium text-gray-900 dark:text-gray-100">{fullName}</p>}
            <nav className="flex flex-col gap-3 text-sm text-gray-600 dark:text-gray-400">
              <Link href="/help" onClick={() => setMenuOpen(false)} className="hover:text-gray-900 dark:hover:text-gray-100">
                {t("help")}
              </Link>
              <Link href="/settings" onClick={() => setMenuOpen(false)} className="hover:text-gray-900 dark:hover:text-gray-100">
                {t("accountSettings")}
              </Link>
            </nav>
          </div>

          <div className="mt-4 flex items-center gap-3">
            <LanguageSwitcher />
            <ThemeSwitcher initialTheme={initialTheme} />
          </div>

          <div className="mt-4">
            <Button variant="secondary" onClick={handleLogout}>
              {t("logout")}
            </Button>
          </div>
        </div>
      )}
    </header>
  );
}
