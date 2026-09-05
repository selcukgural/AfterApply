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
