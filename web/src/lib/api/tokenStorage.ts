import type { UserProfileResponse } from "@/types/api";

// Since 2026-09-27 no token is kept where a script can read it: the refresh token is an HttpOnly
// cookie on the API's host and the access token lives in memory (authStore). What stays here is
// the signed-in user's profile — a hint that lets a reload render the right header at once and
// tells the app a session is worth refreshing. It grants nothing on its own.
const USER_KEY = "aa_user";

// Where sessions were kept before. Read once, so a browser signed in back then trades its refresh
// token for the cookie instead of being signed out, and then removed. Can go once those tokens
// have expired (DECISIONS.md 2026-09-27).
const LEGACY_KEYS = {
  accessToken: "aa_access_token",
  accessTokenExpiresAt: "aa_access_token_expires_at",
  refreshToken: "aa_refresh_token",
  refreshTokenExpiresAt: "aa_refresh_token_expires_at",
} as const;

function isBrowser(): boolean {
  return typeof window !== "undefined";
}

export const tokenStorage = {
  getUser(): UserProfileResponse | null {
    if (!isBrowser()) return null;

    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;

    try {
      return JSON.parse(raw) as UserProfileResponse;
    } catch {
      return null;
    }
  },

  setUser(user: UserProfileResponse): void {
    if (!isBrowser()) return;

    localStorage.setItem(USER_KEY, JSON.stringify(user));
  },

  legacyRefreshToken(): string | null {
    if (!isBrowser()) return null;

    return localStorage.getItem(LEGACY_KEYS.refreshToken);
  },

  clearLegacy(): void {
    if (!isBrowser()) return;

    Object.values(LEGACY_KEYS).forEach((key) => localStorage.removeItem(key));
  },

  clear(): void {
    if (!isBrowser()) return;

    localStorage.removeItem(USER_KEY);
    this.clearLegacy();
  },
};
