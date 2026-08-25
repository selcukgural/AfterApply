"use client";

import { useLocale } from "next-intl";
import { usePathname, useRouter } from "@/i18n/navigation";
import { routing } from "@/i18n/routing";
import { useAuth } from "@/lib/auth/AuthContext";
import { authApi } from "@/lib/api/auth";

export function LanguageSwitcher() {
  const locale = useLocale();
  const pathname = usePathname();
  const router = useRouter();
  const { isAuthenticated } = useAuth();

  const handleSwitch = (code: (typeof routing.locales)[number]) => {
    router.replace(pathname, { locale: code });
    if (isAuthenticated) {
      // Persists the choice to the account so it's applied on the next
      // login from any device/browser, not just remembered via this
      // browser's NEXT_LOCALE cookie. Fire-and-forget: a transient failure
      // here shouldn't block the (already-completed) navigation.
      void authApi.updateLanguage(code);
    }
  };

  return (
    <div className="flex items-center gap-1 text-sm text-gray-600 dark:text-gray-400">
      {routing.locales.map((code) => (
        <button
          key={code}
          type="button"
          onClick={() => handleSwitch(code)}
          disabled={code === locale}
          className={
            code === locale
              ? "font-semibold text-gray-900 dark:text-gray-100"
              : "text-gray-500 hover:text-gray-900 dark:text-gray-500 dark:hover:text-gray-100"
          }
        >
          {code.toUpperCase()}
        </button>
      ))}
    </div>
  );
}
