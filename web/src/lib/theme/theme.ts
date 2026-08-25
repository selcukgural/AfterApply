export type Theme = "light" | "dark";

const THEME_COOKIE = "theme";

// No React context/provider here on purpose: unlike locale, theme isn't part
// of the URL, so a switch is a pure DOM+cookie side effect — no navigation or
// component re-render coordination is needed.
export function applyTheme(theme: Theme): void {
  document.documentElement.classList.toggle("dark", theme === "dark");
  document.cookie = `${THEME_COOKIE}=${theme}; path=/; max-age=31536000; samesite=lax`;
}

// Used only to sync a pre-registration theme choice onto a brand-new account
// (see register/page.tsx) — reads the cookie a still-anonymous visitor may
// have already set via the theme switcher.
export function getStoredThemeCookie(): Theme | null {
  const match = document.cookie.match(/(?:^|; )theme=(light|dark)(?:;|$)/);
  return match ? (match[1] as Theme) : null;
}
