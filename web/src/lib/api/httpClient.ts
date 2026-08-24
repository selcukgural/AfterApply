import type { AuthResponse } from "@/types/api";
import { authStore } from "./authStore";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5151";

// Endpoints that must never get an Authorization header attached and must
// never themselves trigger a refresh-on-401 retry (that would loop).
const NO_AUTH_ENDPOINTS = ["/api/auth/login", "/api/auth/register", "/api/auth/refresh"];

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

async function refreshAccessToken(): Promise<AuthResponse> {
  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = (async () => {
    const refreshToken = authStore.getRefreshToken();
    if (!refreshToken) {
      throw new ApiError(401, "No refresh token available");
    }

    const response = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
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

function isNoAuthEndpoint(path: string): boolean {
  return NO_AUTH_ENDPOINTS.some((endpoint) => path.startsWith(endpoint));
}

async function performFetch(path: string, options: RequestInit): Promise<Response> {
  const headers = new Headers(options.headers);
  if (!headers.has("Content-Type") && options.body) {
    headers.set("Content-Type", "application/json");
  }

  const token = authStore.getAccessToken();
  if (token && !isNoAuthEndpoint(path)) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  return fetch(`${API_BASE_URL}${path}`, { ...options, headers });
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
        window.location.href = "/login";
      }
      throw new ApiError(401, "Oturum süresi doldu, tekrar giriş yapın.");
    }
  }

  if (!response.ok) {
    const body = await response.json().catch(() => undefined);
    const message =
      (body && typeof body === "object" && "detail" in body && String(body.detail)) ||
      (body && typeof body === "object" && "title" in body && String(body.title)) ||
      "İstek başarısız oldu.";
    throw new ApiError(response.status, message, body);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
