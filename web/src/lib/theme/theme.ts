export type Theme = "light" | "dark";

const THEME_COOKIE = "theme";

/**
 * Runs inline in `<head>` before anything paints: reads the theme cookie and stamps the `dark`
 * class on `<html>`. This replaced reading the cookie on the server (2026-09-14) — `cookies()` in
 * the root layout made every page dynamic, this costs nothing and keeps them static. Kept to one
 * expression, no dependencies, wrapped in try/catch so a browser with cookies disabled just gets
 * the light default.
 */
export const THEME_BOOT_SCRIPT =
  '(function(){try{if(/(?:^|; )theme=dark(?:;|$)/.test(document.cookie))document.documentElement.classList.add("dark")}catch(e){}})()';

// No React context/provider here on purpose: unlike locale, theme isn't part
// of the URL, so a switch is a pure DOM+cookie side effect — no navigation or
// component re-render coordination is needed.
export function applyTheme(theme: Theme): void {
  document.documentElement.classList.toggle("dark", theme === "dark");
  document.cookie = `${THEME_COOKIE}=${theme}; path=/; max-age=31536000; samesite=lax`;
}

/** The theme the document is currently showing — what the boot script or applyTheme last set. */
export function readDocumentTheme(): Theme {
  return document.documentElement.classList.contains("dark") ? "dark" : "light";
}

// Used only to sync a pre-registration theme choice onto a brand-new account
// (see register/page.tsx) — reads the cookie a still-anonymous visitor may
// have already set via the theme switcher.
export function getStoredThemeCookie(): Theme | null {
  const match = document.cookie.match(/(?:^|; )theme=(light|dark)(?:;|$)/);
  return match ? (match[1] as Theme) : null;
}
