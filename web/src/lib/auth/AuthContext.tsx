"use client";

import { createContext, useCallback, useContext, useEffect, useState, useSyncExternalStore } from "react";
import type { AuthResponse, GoogleSignInResponse, UserProfileResponse } from "@/types/api";
import {
  authApi,
  type GoogleSignInRequest,
  type GoogleSignupRequest,
  type LoginRequest,
  type RegisterRequest,
} from "@/lib/api/auth";
import { authStore } from "@/lib/api/authStore";

interface AuthContextValue {
  user: UserProfileResponse | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  // Return the full auth response (not void) so callers can read
  // `user.preferredLanguage` right after login/register and redirect to the
  // account's saved language — see login/register pages.
  login: (request: LoginRequest) => Promise<AuthResponse>;
  register: (request: RegisterRequest) => Promise<AuthResponse>;
  // Stores the session only when the response carries one; a `pendingSignup` result leaves the
  // store untouched until completeGoogleSignup creates the account.
  signInWithGoogle: (request: GoogleSignInRequest) => Promise<GoogleSignInResponse>;
  completeGoogleSignup: (request: GoogleSignupRequest) => Promise<AuthResponse>;
  logout: () => Promise<void>;
  // password is omitted for an account that has none (user.hasPassword === false).
  deleteAccount: (password?: string) => Promise<void>;
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
    return auth;
  }, []);

  const register = useCallback(async (request: RegisterRequest) => {
    const auth = await authApi.register(request);
    authStore.setAuth(auth);
    return auth;
  }, []);

  const signInWithGoogle = useCallback(async (request: GoogleSignInRequest) => {
    const result = await authApi.googleSignIn(request);
    if (result.auth) {
      authStore.setAuth(result.auth);
    }
    return result;
  }, []);

  const completeGoogleSignup = useCallback(async (request: GoogleSignupRequest) => {
    const auth = await authApi.googleSignup(request);
    authStore.setAuth(auth);
    return auth;
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

  const deleteAccount = useCallback(async (password?: string) => {
    await authApi.deleteAccount(password === undefined ? {} : { password });
    authStore.clear();
  }, []);

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: user !== null,
        isLoading,
        login,
        register,
        signInWithGoogle,
        completeGoogleSignup,
        logout,
        deleteAccount,
      }}
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
