"use client";

import { createContext, useCallback, useContext, useEffect, useState, useSyncExternalStore } from "react";
import type { UserProfileResponse } from "@/types/api";
import { authApi, type LoginRequest, type RegisterRequest } from "@/lib/api/auth";
import { authStore } from "@/lib/api/authStore";

interface AuthContextValue {
  user: UserProfileResponse | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (request: LoginRequest) => Promise<void>;
  register: (request: RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function getServerSnapshot(): UserProfileResponse | null {
  return null;
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  // authStore is an external mutable store (module-level, backed by
  // localStorage) — useSyncExternalStore is the correct React primitive for
  // this, rather than mirroring it into local state via setState-in-effect.
  const user = useSyncExternalStore(authStore.subscribe, authStore.getUser, getServerSnapshot);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    authStore.hydrate();

    const validate = async () => {
      if (authStore.getAccessToken()) {
        try {
          const profile = await authApi.me();
          authStore.updateUser(profile);
        } catch {
          authStore.clear();
        }
      }
      setIsLoading(false);
    };

    void validate();
  }, []);

  const login = useCallback(async (request: LoginRequest) => {
    const auth = await authApi.login(request);
    authStore.setAuth(auth);
  }, []);

  const register = useCallback(async (request: RegisterRequest) => {
    const auth = await authApi.register(request);
    authStore.setAuth(auth);
  }, []);

  const logout = useCallback(async () => {
    const refreshToken = authStore.getRefreshToken();
    try {
      if (refreshToken) {
        await authApi.logout(refreshToken);
      }
    } finally {
      authStore.clear();
    }
  }, []);

  return (
    <AuthContext.Provider
      value={{ user, isAuthenticated: user !== null, isLoading, login, register, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
