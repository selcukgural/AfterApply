import type { AuthResponse, UserProfileResponse } from "@/types/api";
import { tokenStorage } from "./tokenStorage";

/**
 * Module-level singleton (not React state) so httpClient.ts can read the
 * current access token synchronously without a render cycle. AuthContext
 * subscribes to this for UI-facing reads.
 *
 * The access token is held here and nowhere else — not in localStorage, where any injected script
 * could read it. A reload starts without one and gets a fresh one from the refresh-token cookie.
 */
let currentAccessToken: string | null = null;
let currentUser: UserProfileResponse | null = null;
const listeners = new Set<() => void>();

function notify(): void {
  listeners.forEach((listener) => listener());
}

export const authStore = {
  hydrate(): void {
    currentUser = tokenStorage.getUser();
    notify();
  },

  setAuth(auth: AuthResponse): void {
    tokenStorage.setUser(auth.user);
    currentAccessToken = auth.accessToken;
    currentUser = auth.user;
    notify();
  },

  updateUser(user: UserProfileResponse): void {
    currentUser = user;
    tokenStorage.setUser(user);
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

  getUser(): UserProfileResponse | null {
    return currentUser;
  },

  subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
};
