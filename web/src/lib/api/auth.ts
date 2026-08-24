import type { AuthResponse, UserProfileResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
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
};
