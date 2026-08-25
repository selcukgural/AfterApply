import type { AuthResponse, UserProfileResponse } from "@/types/api";
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
  password: string;
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
    const filename = contentDisposition?.match(/filename="?([^"]+)"?/)?.[1] ?? "afterapply-export.json";

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
