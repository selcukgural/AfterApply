// Shared chrome.storage.local access for popup.js and options.js. Per-viewer/per-install
// settings only (API base URL + personal access token) — never synced, never sent anywhere
// except as the Authorization header on requests this extension itself makes to that API.

const STORAGE_KEY = "afterapply_settings";
const DEFAULT_API_BASE_URL = "https://api.ekariyerim.com";

export async function getSettings() {
  const result = await chrome.storage.local.get(STORAGE_KEY);
  const settings = result[STORAGE_KEY] ?? {};
  return {
    apiBaseUrl: settings.apiBaseUrl || DEFAULT_API_BASE_URL,
    token: settings.token || "",
    // ISO 8601, or "" when unknown. Known for every token obtained by pairing (the server hands
    // the expiry over with it); empty for one pasted in by hand and for every install that
    // predates pairing — those keep working exactly as before, they just cannot be warned in
    // advance. Tokens expire after PersonalAccessTokens:LifetimeDays whether or not anyone told
    // the extension, which is precisely why the connection used to die without explanation.
    tokenExpiresAt: settings.tokenExpiresAt || "",
  };
}

/** Days left on the stored token, or null when the expiry is unknown/unparseable. Negative means
 * it has already lapsed. */
export function daysUntilExpiry(tokenExpiresAt, now = new Date()) {
  if (!tokenExpiresAt) {
    return null;
  }
  const expiresAt = new Date(tokenExpiresAt);
  if (Number.isNaN(expiresAt.getTime())) {
    return null;
  }
  return Math.floor((expiresAt.getTime() - now.getTime()) / 86_400_000);
}

/** How far ahead the connection starts warning that it is about to lapse. Two weeks is long
 * enough that a person who opens the popup once a week sees it before it bites. */
export const EXPIRY_WARNING_DAYS = 14;

export async function saveSettings(settings) {
  await chrome.storage.local.set({ [STORAGE_KEY]: settings });
}

// Separate key from afterapply_settings on purpose: theme is a display preference (mirrors the
// web app's own "theme" cookie, see web/src/lib/theme/theme.ts), not an API credential, so it's
// kept out of the object that carries the access token. Value is "light" | "dark", or absent —
// absent means "follow the OS prefers-color-scheme", same fallback popup.css implements in CSS.
const THEME_KEY = "afterapply_theme";

export async function getTheme() {
  const result = await chrome.storage.local.get(THEME_KEY);
  return result[THEME_KEY] ?? null;
}

export async function saveTheme(theme) {
  await chrome.storage.local.set({ [THEME_KEY]: theme });
}

// Same shape/rationale as THEME_KEY above, shared across every extension page (popup, options).
// Value is "tr" | "en", or absent — absent means "follow navigator.language", same fallback
// pattern as theme's OS prefers-color-scheme.
const LANGUAGE_KEY = "afterapply_language";

export async function getLanguage() {
  const result = await chrome.storage.local.get(LANGUAGE_KEY);
  return result[LANGUAGE_KEY] ?? null;
}

export async function saveLanguage(language) {
  await chrome.storage.local.set({ [LANGUAGE_KEY]: language });
}

// Off by default — the Gmail content script (gmail-scan.js) checks this before reading anything
// from the page, full stop. Kept as its own flat key rather than folding into afterapply_settings
// so options.js can gate/reset it independently of the token. Note: gmail-scan.js/
// local-filter-config.js can't `import` this helper (Chrome MV3 content scripts have no ES-module
// support), so they read the same key directly via chrome.storage.local — see their own comment.
const GMAIL_SCAN_ENABLED_KEY = "afterapply_gmail_scan_enabled";

export async function getGmailScanEnabled() {
  const result = await chrome.storage.local.get(GMAIL_SCAN_ENABLED_KEY);
  return result[GMAIL_SCAN_ENABLED_KEY] ?? false;
}

export async function setGmailScanEnabled(enabled) {
  await chrome.storage.local.set({ [GMAIL_SCAN_ENABLED_KEY]: enabled });
}
