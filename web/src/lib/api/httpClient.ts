import type { AuthResponse } from "@/types/api";
import { routing } from "@/i18n/routing";
import { authStore } from "./authStore";
import enMessages from "../../../messages/en.json";
import trMessages from "../../../messages/tr.json";

const FALLBACK_MESSAGES: Record<string, { generic: string; sessionExpired: string }> = {
  en: enMessages.errors,
  tr: trMessages.errors,
};

export const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5151";

// Endpoints that must never get an Authorization header attached and must
// never themselves trigger a refresh-on-401 retry (that would loop).
const NO_AUTH_ENDPOINTS = [
  "/api/config",
  "/api/auth/login",
  "/api/auth/register",
  "/api/auth/google",
  "/api/auth/refresh",
  "/api/auth/forgot-password",
  "/api/auth/reset-password",
];

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public body?: unknown,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

// Single-flight refresh: dedupes concurrent 401-triggered refresh attempts
// within this tab. Necessary because the backend revokes ALL of a user's
// refresh tokens if an already-rotated one is reused — firing two parallel
// refresh calls with the same token would lock the user out.
let refreshPromise: Promise<AuthResponse> | null = null;

// This module sits below the React tree (no hook access), so it can't call
// useLocale(). The active locale is always the first path segment because
// routing is configured with localePrefix: "always" — read it straight off
// the URL instead of threading it through every apiFetch call site.
function getCurrentLocale(): string {
  if (typeof window === "undefined") {
    return routing.defaultLocale;
  }
  const segment = window.location.pathname.split("/")[1];
  return (routing.locales as readonly string[]).includes(segment) ? segment : routing.defaultLocale;
}

function isNoAuthEndpoint(path: string): boolean {
  return NO_AUTH_ENDPOINTS.some((endpoint) => path.startsWith(endpoint));
}

// Last-resort fallback only — this module sits below the React tree (no
// useTranslations access), so it can't rely on next-intl's provider. Callers
// should prefer the backend's already-localized detail/errors/title (see
// apiFetch below) and only hit this for a raw network-failure / no-JSON-body
// scenario.
function getFallbackMessage(key: "generic" | "sessionExpired"): string {
  const locale = getCurrentLocale();
  return (FALLBACK_MESSAGES[locale] ?? FALLBACK_MESSAGES[routing.defaultLocale]!)[key];
}

// ASP.NET's Results.ValidationProblem(...) (used by FluentValidation filters
// and the import endpoints) returns HttpValidationProblemDetails: an
// `errors: { [field]: string[] }` map with each message already localized
// server-side via SharedStrings, but no top-level `detail` and a hardcoded
// English `title` ("One or more validation errors occurred."). Surface the
// field messages instead of falling through to that generic English title.
function extractValidationErrorsMessage(body: unknown): string | undefined {
  if (!body || typeof body !== "object" || !("errors" in body)) {
    return undefined;
  }
  const errors = (body as { errors: unknown }).errors;
  if (!errors || typeof errors !== "object") {
    return undefined;
  }
  const messages = Object.values(errors as Record<string, unknown>).flatMap((value) =>
    Array.isArray(value) ? value.filter((item): item is string => typeof item === "string") : [],
  );
  return messages.length > 0 ? messages.join(" ") : undefined;
}

async function performFetch(path: string, options: RequestInit): Promise<Response> {
  const headers = new Headers(options.headers);
  // FormData bodies (file uploads) must NOT get an explicit Content-Type — the
  // browser sets multipart/form-data with the correct boundary itself.
  if (!headers.has("Content-Type") && options.body && !(options.body instanceof FormData)) {
    headers.set("Content-Type", "application/json");
  }
  headers.set("Accept-Language", getCurrentLocale());

  const token = authStore.getAccessToken();
  if (token && !isNoAuthEndpoint(path)) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  return fetch(`${API_BASE_URL}${path}`, { ...options, headers });
}

async function refreshAccessToken(): Promise<AuthResponse> {
  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = (async () => {
    const refreshToken = authStore.getRefreshToken();
    if (!refreshToken) {
      throw new ApiError(401, "No refresh token available");
    }

    const response = await performFetch("/api/auth/refresh", {
      method: "POST",
      body: JSON.stringify({ refreshToken }),
    });

    if (!response.ok) {
      throw new ApiError(response.status, "Refresh failed");
    }

    const auth = (await response.json()) as AuthResponse;
    authStore.setAuth(auth);
    return auth;
  })();

  try {
    return await refreshPromise;
  } finally {
    refreshPromise = null;
  }
}

export async function apiFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response = await performFetch(path, options);

  if (response.status === 401 && !isNoAuthEndpoint(path)) {
    try {
      await refreshAccessToken();
      response = await performFetch(path, options);
    } catch {
      authStore.clear();
      if (typeof window !== "undefined") {
        // Hard navigation is intentional here (not a React event handler, no
        // router available) — it also guarantees all in-memory/query-cache
        // state is wiped on a forced session expiry, not just the URL.
        // eslint-disable-next-line @next/next/no-location-assign-relative-destination
        window.location.href = `/${getCurrentLocale()}/login`;
      }
      throw new ApiError(401, getFallbackMessage("sessionExpired"));
    }
  }

  if (!response.ok) {
    const body = await response.json().catch(() => undefined);
    const message =
      (body && typeof body === "object" && "detail" in body && String(body.detail)) ||
      extractValidationErrorsMessage(body) ||
      (body && typeof body === "object" && "title" in body && String(body.title)) ||
      getFallbackMessage("generic");
    throw new ApiError(response.status, message, body);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
