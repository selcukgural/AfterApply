import type { AuthResponse } from "@/types/api";
import { routing } from "@/i18n/routing";

type Locale = (typeof routing.locales)[number];

// The account's saved language wins right after login/register, regardless of which
// device/browser the user is signing in from — until they explicitly switch again via the
// language switcher. Returns the locale to push /dashboard under, or null to stay on the
// current one.
export function postAuthLocale(auth: AuthResponse, currentLocale: string): Locale | null {
  const preferred = auth.user.preferredLanguage;
  const supported: readonly string[] = routing.locales;
  return supported.includes(preferred) && preferred !== currentLocale ? (preferred as Locale) : null;
}

/**
 * Where to go after a successful sign-in or sign-up. Normally the dashboard — the exception is
 * someone who arrived mid-task and has to be handed back to it, which today means exactly one
 * page: the browser extension's pairing confirmation, opened by the extension itself for a person
 * who may not have had an account thirty seconds ago.
 *
 * This is an allowlist of one shape, not a sanitiser over arbitrary input, and that is the point.
 * A `?next=` parameter that accepts any path is an open redirect waiting to happen — and a
 * credential-shaped flow is precisely where one would be worth exploiting. Anything that is not
 * "/pair" (optionally with a pairing code) or "/my-reviews/write" (optionally with a company slug)
 * becomes null and the user lands on the dashboard.
 */
const RETURN_TO_PATTERNS = [
  /^\/pair(\?code=[A-Za-z0-9-]{1,16})?$/,
  // The second shape (2026-09-13): someone who clicked "write a review" on a public company page
  // and had to sign in first. The slug is the company's URL segment — lowercase ascii and hyphens,
  // the same alphabet CompanySlugGenerator emits — and nothing else may follow it.
  /^\/my-reviews\/write(\?company=[a-z0-9][a-z0-9-]{0,99})?$/,
];

export function sanitizeReturnTo(raw: string | null | undefined): string | null {
  return raw && RETURN_TO_PATTERNS.some((pattern) => pattern.test(raw)) ? raw : null;
}

/** The `?next=` of the page currently open, if it is one of the shapes above. */
export function returnToFromLocation(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  return sanitizeReturnTo(new URLSearchParams(window.location.search).get("next"));
}

/** The destination itself: the pending return, or the dashboard. */
export function postAuthDestination(returnTo: string | null | undefined): string {
  return sanitizeReturnTo(returnTo) ?? "/dashboard";
}
