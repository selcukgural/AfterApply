import type { AuthResponse, GoogleSignInResponse, LinkedInSignInResponse, UserProfileResponse } from "@/types/api";
import { API_BASE_URL, apiFetch } from "./httpClient";
import { authStore } from "./authStore";

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  consentAccepted: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
}

export interface DeleteAccountRequest {
  // Omitted for an account without a password (user.hasPassword === false).
  password?: string;
}

// What the Google callback page posts once accounts.google.com redirected back (see
// lib/auth/googleOAuth.ts for where code/codeVerifier/redirectUri come from).
export interface GoogleSignInRequest {
  code: string;
  codeVerifier: string;
  redirectUri: string;
}

export interface GoogleSignupRequest {
  signupToken: string;
  firstName: string;
  lastName: string;
  consentAccepted: boolean;
}

// What the LinkedIn callback page posts once linkedin.com redirected back (see
// lib/auth/linkedinOAuth.ts). No PKCE verifier — LinkedIn's flow doesn't use one.
export interface LinkedInSignInRequest {
  code: string;
  redirectUri: string;
}

export interface LinkedInSignupRequest {
  signupToken: string;
  firstName: string;
  lastName: string;
  // Only sent when the prefill carried no email (LinkedIn provided none); the API ignores it
  // otherwise and uses the verified address from the signup token.
  email?: string;
  consentAccepted: boolean;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

export const authApi = {
  register: (request: RegisterRequest) =>
    apiFetch<AuthResponse>("/api/auth/register", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  login: (request: LoginRequest) =>
    apiFetch<AuthResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  googleSignIn: (request: GoogleSignInRequest) =>
    apiFetch<GoogleSignInResponse>("/api/auth/google", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  googleSignup: (request: GoogleSignupRequest) =>
    apiFetch<AuthResponse>("/api/auth/google/signup", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  linkedInSignIn: (request: LinkedInSignInRequest) =>
    apiFetch<LinkedInSignInResponse>("/api/auth/linkedin", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  linkedInSignup: (request: LinkedInSignupRequest) =>
    apiFetch<AuthResponse>("/api/auth/linkedin/signup", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  logout: (refreshToken: string) =>
    apiFetch<void>("/api/auth/logout", {
      method: "POST",
      body: JSON.stringify({ refreshToken }),
    }),

  me: () => apiFetch<UserProfileResponse>("/api/users/me"),

  updateProfile: (request: UpdateProfileRequest) =>
    apiFetch<UserProfileResponse>("/api/users/me", {
      method: "PUT",
      body: JSON.stringify(request),
    }),

  updateLanguage: (language: string) =>
    apiFetch<UserProfileResponse>("/api/users/me/language", {
      method: "PUT",
      body: JSON.stringify({ language }),
    }),

  updateTheme: (theme: string) =>
    apiFetch<UserProfileResponse>("/api/users/me/theme", {
      method: "PUT",
      body: JSON.stringify({ theme }),
    }),

  deleteAccount: (request: DeleteAccountRequest) =>
    apiFetch<void>("/api/users/me", {
      method: "DELETE",
      body: JSON.stringify(request),
    }),

  // Always resolves on a 2xx (204) — the backend returns the same response whether or not the
  // email is registered, so there's nothing meaningful to branch on here either.
  forgotPassword: (request: ForgotPasswordRequest) =>
    apiFetch<void>("/api/auth/forgot-password", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  resetPassword: (request: ResetPasswordRequest) =>
    apiFetch<void>("/api/auth/reset-password", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  // Not routed through apiFetch: the response body is a file download (raw
  // bytes), not JSON to parse into a typed object.
  exportData: async (): Promise<void> => {
    const token = authStore.getAccessToken();
    const response = await fetch(`${API_BASE_URL}/api/users/me/export`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });

    if (!response.ok) {
      // Not an ApiError on purpose: this endpoint returns a raw file body, not
      // a JSON ProblemDetails, so there's no backend-localized message to
      // surface — callers should show their own translated fallback instead
      // of this technical message.
      throw new Error(`Export failed with status ${response.status}`);
    }

    const blob = await response.blob();
    const contentDisposition = response.headers.get("Content-Disposition");
    const filename = contentDisposition?.match(/filename="?([^"]+)"?/)?.[1] ?? "e-kariyerim-data-export.json";

    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  },
};
