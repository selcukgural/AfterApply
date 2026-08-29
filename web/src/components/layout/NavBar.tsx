"use client";

import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { Button } from "@/components/ui/Button";
import { Logo } from "@/components/layout/Logo";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import type { Theme } from "@/lib/theme/theme";

export function NavBar({ initialTheme }: { initialTheme: Theme }) {
  const { user, logout } = useAuth();
  const router = useRouter();
  const t = useTranslations("nav");

  const handleLogout = async () => {
    await logout();
    router.replace("/login");
  };

  return (
    <header className="border-b border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
      <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
        <div className="flex items-center gap-6">
          <Link href="/dashboard">
            <Logo />
          </Link>
          <nav className="flex gap-4 text-sm text-gray-600 dark:text-gray-400">
            <Link href="/dashboard" className="hover:text-gray-900 dark:hover:text-gray-100">
              {t("dashboard")}
            </Link>
            <Link href="/applications" className="hover:text-gray-900 dark:hover:text-gray-100">
              {t("applications")}
            </Link>
            <Link href="/tracked-jobs" className="hover:text-gray-900 dark:hover:text-gray-100">
              {t("trackedJobs")}
            </Link>
            <Link href="/import" className="hover:text-gray-900 dark:hover:text-gray-100">
              {t("import")}
            </Link>
          </nav>
        </div>
        <div className="flex items-center gap-3 text-sm text-gray-600 dark:text-gray-400">
          {user && (
            <span>
              {user.firstName} {user.lastName}
            </span>
          )}
          <Link href="/settings" className="hover:text-gray-900 dark:hover:text-gray-100">
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
