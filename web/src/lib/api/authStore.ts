import type { AuthResponse, UserProfileResponse } from "@/types/api";
import { tokenStorage } from "./tokenStorage";

/**
 * Module-level singleton (not React state) so httpClient.ts can read the
 * current access token synchronously without a render cycle. AuthContext
 * subscribes to this for UI-facing reads.
 */
let currentAccessToken: string | null = null;
let currentUser: UserProfileResponse | null = null;
const listeners = new Set<() => void>();

function notify(): void {
  listeners.forEach((listener) => listener());
}

export const authStore = {
  hydrate(): void {
    const stored = tokenStorage.get();
    currentAccessToken = stored?.accessToken ?? null;
    currentUser = stored?.user ?? null;
    notify();
  },

  setAuth(auth: AuthResponse): void {
    tokenStorage.set(auth);
    currentAccessToken = auth.accessToken;
    currentUser = auth.user;
    notify();
  },

  updateUser(user: UserProfileResponse): void {
    currentUser = user;
    const stored = tokenStorage.get();
    if (stored) {
      tokenStorage.set({ ...stored, user });
    }
    notify();
  },

  clear(): void {
    tokenStorage.clear();
    currentAccessToken = null;
    currentUser = null;
    notify();
  },

  getAccessToken(): string | null {
    return currentAccessToken;
  },

  getRefreshToken(): string | null {
    return tokenStorage.get()?.refreshToken ?? null;
  },

  getUser(): UserProfileResponse | null {
    return currentUser;
  },

  subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
};
