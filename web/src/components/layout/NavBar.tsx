"use client";

import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { useSuggestionCount } from "@/hooks/useSuggestionCount";
import { Button } from "@/components/ui/Button";
import { Logo } from "@/components/layout/Logo";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import type { Theme } from "@/lib/theme/theme";

export function NavBar({ initialTheme }: { initialTheme: Theme }) {
  const { user, logout } = useAuth();
  const router = useRouter();
  const t = useTranslations("nav");
  const { data: suggestionCount } = useSuggestionCount();

  const handleLogout = async () => {
    await logout();
    router.replace("/login");
  };

  return (
    <header className="border-b border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
      <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-y-2 px-4 py-3">
        <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
          <Link href="/dashboard">
            <Logo />
          </Link>
          <nav className="flex flex-wrap gap-x-4 gap-y-2 text-sm text-gray-600 dark:text-gray-400">
            <Link href="/dashboard" className="whitespace-nowrap hover:text-gray-900 dark:hover:text-gray-100">
              {t("dashboard")}
            </Link>
            <Link href="/applications" className="whitespace-nowrap hover:text-gray-900 dark:hover:text-gray-100">
              {t("applications")}
            </Link>
            <Link href="/tracked-jobs" className="whitespace-nowrap hover:text-gray-900 dark:hover:text-gray-100">
              {t("trackedJobs")}
            </Link>
            <Link href="/import" className="whitespace-nowrap hover:text-gray-900 dark:hover:text-gray-100">
              {t("import")}
            </Link>
            <Link
              href="/suggestions"
              className="flex items-center gap-1.5 whitespace-nowrap hover:text-gray-900 dark:hover:text-gray-100"
            >
              {t("suggestions")}
              {!!suggestionCount && (
                <span className="inline-flex min-w-[1.25rem] items-center justify-center rounded-full bg-blue-600 px-1.5 py-0.5 text-xs font-semibold leading-none text-white">
                  {suggestionCount}
                </span>
              )}
            </Link>
          </nav>
        </div>
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2 text-sm text-gray-600 dark:text-gray-400">
          {user && (
            <span className="whitespace-nowrap">
              {user.firstName} {user.lastName}
            </span>
          )}
          <Link href="/help" className="whitespace-nowrap hover:text-gray-900 dark:hover:text-gray-100">
            {t("help")}
          </Link>
          <Link href="/settings" className="whitespace-nowrap hover:text-gray-900 dark:hover:text-gray-100">
            {t("accountSettings")}
          </Link>
          <Button variant="secondary" onClick={handleLogout}>
            {t("logout")}
          </Button>
          <LanguageSwitcher />
          <ThemeSwitcher initialTheme={initialTheme} />
        </div>
      </div>
    </header>
  );
}
