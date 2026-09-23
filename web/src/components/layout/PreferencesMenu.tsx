"use client";

import { useEffect, useId, useRef, useState, type ReactNode } from "react";
import { useLocale, useTranslations } from "next-intl";
import { routing } from "@/i18n/routing";
import { useDocumentTheme } from "@/hooks/useDocumentTheme";
import { useSwitchLanguage, type Locale } from "@/components/layout/LanguageSwitcher";
import { THEMES, useSwitchTheme } from "@/components/layout/ThemeSwitcher";
import type { Theme } from "@/lib/theme/theme";

/** Each language by its own name — someone who cannot read the page's language can still find theirs. */
const LANGUAGE_NAMES: Record<Locale, string> = { tr: "Türkçe", en: "English" };

function GlobeIcon({ className = "h-[17px] w-[17px]" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={1.8} aria-hidden="true">
      <circle cx="12" cy="12" r="9" />
      <path d="M3 12h18M12 3a14 14 0 010 18M12 3a14 14 0 000 18" />
    </svg>
  );
}

function ThemeIcon({ theme, className = "h-4 w-4" }: { theme: Theme; className?: string }) {
  return theme === "dark" ? (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={1.8} aria-hidden="true">
      <path strokeLinecap="round" strokeLinejoin="round" d="M21 12.8A9 9 0 1111.2 3a7 7 0 009.8 9.8z" />
    </svg>
  ) : (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={1.8} aria-hidden="true">
      <circle cx="12" cy="12" r="4" />
      <path strokeLinecap="round" d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
    </svg>
  );
}

function Segmented<T extends string>({
  label,
  options,
  value,
  onChange,
  inline,
}: {
  label: string;
  options: readonly { value: T; content: ReactNode; lang?: string }[];
  value: T;
  onChange: (value: T) => void;
  inline: boolean;
}) {
  const id = useId();
  return (
    <div className={inline ? "flex items-center justify-between gap-3" : "flex flex-col gap-1.5"}>
      <span id={id} className="text-xs font-semibold text-gray-500 dark:text-gray-400">
        {label}
      </span>
      <div
        role="group"
        aria-labelledby={id}
        className={`flex gap-0.5 rounded-md bg-gray-100 p-0.5 dark:bg-gray-800 ${inline ? "w-56" : ""}`}
      >
        {options.map((option) => {
          const selected = option.value === value;
          return (
            <button
              key={option.value}
              type="button"
              lang={option.lang}
              aria-pressed={selected}
              onClick={() => onChange(option.value)}
              className={`flex h-8 flex-1 items-center justify-center gap-1.5 rounded px-3 text-[13px] font-semibold whitespace-nowrap transition-colors ${
                selected
                  ? "bg-white text-gray-900 shadow-sm dark:bg-gray-950 dark:text-gray-100"
                  : "text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100"
              }`}
            >
              {option.content}
            </button>
          );
        })}
      </div>
    </div>
  );
}

/**
 * Language and theme as two labelled choices. The popover stacks them; the phone menu lays each on
 * one row (`inline`). Both write through the same hooks as the inline switchers, so a signed-in
 * visitor's choice still lands on the account.
 */
export function PreferencesControls({ inline = false }: { inline?: boolean }) {
  const t = useTranslations("siteNav");
  const tTheme = useTranslations("theme");
  const locale = useLocale() as Locale;
  const theme = useDocumentTheme();
  const switchLanguage = useSwitchLanguage();
  const switchTheme = useSwitchTheme();

  return (
    <div className="flex flex-col gap-3">
      <Segmented
        label={t("preferencesLanguage")}
        inline={inline}
        value={locale}
        onChange={(code) => code !== locale && switchLanguage(code)}
        options={routing.locales.map((code) => ({ value: code, content: LANGUAGE_NAMES[code], lang: code }))}
      />
      <Segmented
        label={t("preferencesTheme")}
        inline={inline}
        value={theme}
        onChange={(next) => next !== theme && switchTheme(next)}
        options={THEMES.map((code) => ({
          value: code,
          content: (
            <>
              <ThemeIcon theme={code} />
              {tTheme(code)}
            </>
          ),
        }))}
      />
    </div>
  );
}

/**
 * The signed-out header's one button for language and theme (header canvas "Üst menü · son hâli",
 * D1, 2026-09-24). It replaced the "TR EN · Açık Koyu" pair, which took about 130px of a row that
 * no longer fitted: the globe and the current language say what it is, the sun or moon what the
 * theme is. A signed-in visitor has the same two choices in their user menu.
 */
export function PreferencesMenu() {
  const t = useTranslations("siteNav");
  const locale = useLocale();
  const theme = useDocumentTheme();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const panelId = useId();

  useEffect(() => {
    if (!open) return;
    const handlePointerDown = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) setOpen(false);
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
        aria-controls={panelId}
        aria-label={t("preferences")}
        title={t("preferences")}
        className={`flex h-9 items-center gap-1.5 rounded-md border px-2.5 text-[13px] font-semibold text-gray-600 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-800 ${
          open ? "border-gray-200 bg-gray-100 dark:border-gray-700 dark:bg-gray-800" : "border-transparent"
        }`}
      >
        <GlobeIcon />
        {locale.toUpperCase()}
        <span className="text-gray-400 dark:text-gray-500">
          <ThemeIcon theme={theme} className="h-3.5 w-3.5" />
        </span>
      </button>

      {open && (
        <div
          id={panelId}
          role="dialog"
          aria-label={t("preferences")}
          className="absolute right-0 z-50 mt-2 w-64 rounded-lg border border-gray-200 bg-white p-3.5 shadow-lg dark:border-gray-800 dark:bg-gray-900"
        >
          <PreferencesControls />
        </div>
      )}
    </div>
  );
}
