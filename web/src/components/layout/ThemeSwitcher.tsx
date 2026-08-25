"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { useAuth } from "@/lib/auth/AuthContext";
import { authApi } from "@/lib/api/auth";
import { applyTheme, type Theme } from "@/lib/theme/theme";

const THEMES: Theme[] = ["light", "dark"];

interface ThemeSwitcherProps {
  // Passed from a server component ancestor that already read the `theme`
  // cookie — next-intl's useLocale() gives LanguageSwitcher this for free,
  // there's no equivalent for a hand-rolled preference, so it's threaded
  // through as a prop to keep the initial render consistent with the
  // cookie-driven class already stamped on <html> during SSR.
  initialTheme: Theme;
}

export function ThemeSwitcher({ initialTheme }: ThemeSwitcherProps) {
  const [theme, setTheme] = useState<Theme>(initialTheme);
  const { isAuthenticated } = useAuth();
  const t = useTranslations("theme");

  const handleSwitch = (next: Theme) => {
    applyTheme(next);
    setTheme(next);
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
