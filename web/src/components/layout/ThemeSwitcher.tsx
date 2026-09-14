"use client";

import { useTranslations } from "next-intl";
import { useAuth } from "@/lib/auth/AuthContext";
import { authApi } from "@/lib/api/auth";
import { useDocumentTheme } from "@/hooks/useDocumentTheme";
import { applyTheme, type Theme } from "@/lib/theme/theme";

const THEMES: Theme[] = ["light", "dark"];

/**
 * The server renders this with "light" selected while the page itself is already in the right
 * theme (the boot script in the root layout stamps `<html>` before first paint); on the client the
 * highlight follows the class on `<html>`. That one render of lag on the highlight is the price of
 * not reading the cookie on the server, which is what keeps the public pages static.
 */
export function ThemeSwitcher() {
  const theme = useDocumentTheme();
  const { isAuthenticated } = useAuth();
  const t = useTranslations("theme");

  const handleSwitch = (next: Theme) => {
    applyTheme(next);
    if (isAuthenticated) {
      // Persists the choice to the account so it's applied on the next
      // login from any device/browser, not just remembered via this
      // browser's cookie. Fire-and-forget: a transient failure here
      // shouldn't block the (already-applied) theme switch.
      void authApi.updateTheme(next);
    }
  };

  return (
    <div className="flex items-center gap-1 text-sm text-gray-600 dark:text-gray-400">
      {THEMES.map((code) => (
        <button
          key={code}
          type="button"
          onClick={() => handleSwitch(code)}
          disabled={code === theme}
          className={
            code === theme
              ? "font-semibold text-gray-900 dark:text-gray-100"
              : "text-gray-500 hover:text-gray-900 dark:text-gray-500 dark:hover:text-gray-100"
          }
        >
          {t(code)}
        </button>
      ))}
    </div>
  );
}
