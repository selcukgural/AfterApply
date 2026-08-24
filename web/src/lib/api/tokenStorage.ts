import type { AuthResponse, UserProfileResponse } from "@/types/api";

const KEYS = {
  accessToken: "aa_access_token",
  accessTokenExpiresAt: "aa_access_token_expires_at",
  refreshToken: "aa_refresh_token",
  refreshTokenExpiresAt: "aa_refresh_token_expires_at",
  user: "aa_user",
} as const;

export interface StoredAuth {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: UserProfileResponse;
}

function isBrowser(): boolean {
  return typeof window !== "undefined";
}

export const tokenStorage = {
  get(): StoredAuth | null {
    if (!isBrowser()) return null;

    const accessToken = localStorage.getItem(KEYS.accessToken);
    const accessTokenExpiresAt = localStorage.getItem(KEYS.accessTokenExpiresAt);
    const refreshToken = localStorage.getItem(KEYS.refreshToken);
    const refreshTokenExpiresAt = localStorage.getItem(KEYS.refreshTokenExpiresAt);
    const userRaw = localStorage.getItem(KEYS.user);

    if (!accessToken || !refreshToken || !userRaw) return null;

    try {
      const user = JSON.parse(userRaw) as UserProfileResponse;
      return {
        accessToken,
        accessTokenExpiresAt: accessTokenExpiresAt ?? "",
        refreshToken,
        refreshTokenExpiresAt: refreshTokenExpiresAt ?? "",
        user,
      };
    } catch {
      return null;
    }
  },

  set(auth: AuthResponse): void {
    if (!isBrowser()) return;

    localStorage.setItem(KEYS.accessToken, auth.accessToken);
    localStorage.setItem(KEYS.accessTokenExpiresAt, auth.accessTokenExpiresAt);
    localStorage.setItem(KEYS.refreshToken, auth.refreshToken);
    localStorage.setItem(KEYS.refreshTokenExpiresAt, auth.refreshTokenExpiresAt);
    localStorage.setItem(KEYS.user, JSON.stringify(auth.user));
  },

  clear(): void {
    if (!isBrowser()) return;

    Object.values(KEYS).forEach((key) => localStorage.removeItem(key));
  },
};
