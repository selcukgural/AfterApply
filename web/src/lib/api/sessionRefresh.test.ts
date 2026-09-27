import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// The session model since 2026-09-27: the refresh token is an HttpOnly cookie the browser sends on
// its own, the access token lives in memory, and localStorage holds only the profile hint. These
// tests drive httpClient/authStore against a stubbed fetch and localStorage (the suite runs in
// Node, no jsdom).

const USER = {
  id: "00000000-0000-0000-0000-000000000001",
  email: "a@example.com",
  firstName: "A",
  lastName: "B",
};

function authBody(accessToken: string) {
  return { accessToken, accessTokenExpiresAt: "2026-09-27T12:00:00Z", user: USER };
}

function json(status: number, body?: unknown): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

class MemoryStorage {
  private items = new Map<string, string>();
  getItem(key: string) {
    return this.items.get(key) ?? null;
  }
  setItem(key: string, value: string) {
    this.items.set(key, value);
  }
  removeItem(key: string) {
    this.items.delete(key);
  }
  keys() {
    return [...this.items.keys()];
  }
}

let storage: MemoryStorage;
let fetchMock: ReturnType<typeof vi.fn>;

async function load() {
  vi.resetModules();
  const httpClient = await import("./httpClient");
  const { authStore } = await import("./authStore");
  return { ...httpClient, authStore };
}

beforeEach(() => {
  storage = new MemoryStorage();
  fetchMock = vi.fn();
  vi.stubGlobal("localStorage", storage);
  vi.stubGlobal("window", { location: { pathname: "/tr/dashboard", href: "" } });
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("refreshAccessToken", () => {
  it("spends the cookie with a credentialed request and keeps the new token in memory only", async () => {
    const { refreshAccessToken, authStore } = await load();
    fetchMock.mockResolvedValueOnce(json(200, authBody("fresh")));

    await refreshAccessToken();

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toMatch(/\/api\/auth\/refresh$/);
    expect(init.credentials).toBe("include");
    expect(init.body).toBe("{}");
    expect(authStore.getAccessToken()).toBe("fresh");
    expect(storage.keys()).toEqual(["aa_user"]);
  });

  it("trades a token left in localStorage by an older session for the cookie, then deletes it", async () => {
    storage.setItem("aa_refresh_token", "legacy-token");
    storage.setItem("aa_access_token", "legacy-access");
    const { refreshAccessToken } = await load();
    fetchMock.mockResolvedValueOnce(json(200, authBody("fresh")));

    await refreshAccessToken();

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(JSON.parse(init.body as string)).toEqual({ refreshToken: "legacy-token" });
    expect(storage.getItem("aa_refresh_token")).toBeNull();
    expect(storage.getItem("aa_access_token")).toBeNull();
  });

  it("keeps a legacy token when the refresh failed for a reason other than a dead session", async () => {
    storage.setItem("aa_refresh_token", "legacy-token");
    const { refreshAccessToken } = await load();
    fetchMock.mockResolvedValueOnce(json(429));

    await expect(refreshAccessToken()).rejects.toMatchObject({ status: 429 });
    expect(storage.getItem("aa_refresh_token")).toBe("legacy-token");
  });

  it("retries once when another tab rotated the cookie a moment ago", async () => {
    const { refreshAccessToken, authStore } = await load();
    fetchMock.mockResolvedValueOnce(json(409, { code: "AUTH_REFRESH_SUPERSEDED" }));
    fetchMock.mockResolvedValueOnce(json(200, authBody("second")));

    await refreshAccessToken();

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(authStore.getAccessToken()).toBe("second");
  });

  it("shares one request between concurrent callers in a tab", async () => {
    const { refreshAccessToken } = await load();
    fetchMock.mockResolvedValueOnce(json(200, authBody("fresh")));

    await Promise.all([refreshAccessToken(), refreshAccessToken()]);

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});

describe("apiFetch on a 401", () => {
  it("refreshes and retries with the new token", async () => {
    const { apiFetch } = await load();
    fetchMock
      .mockResolvedValueOnce(json(401))
      .mockResolvedValueOnce(json(200, authBody("fresh")))
      .mockResolvedValueOnce(json(200, { ok: true }));

    await expect(apiFetch("/api/applications")).resolves.toEqual({ ok: true });

    const [, retry] = fetchMock.mock.calls[2] as [string, RequestInit];
    expect(new Headers(retry.headers).get("Authorization")).toBe("Bearer fresh");
    // Only the auth routes carry cookies.
    expect(retry.credentials).toBeUndefined();
  });

  it("signs out when the session is refused", async () => {
    const { apiFetch, authStore } = await load();
    authStore.updateUser(USER as never);
    fetchMock.mockResolvedValueOnce(json(401)).mockResolvedValueOnce(json(401));

    await expect(apiFetch("/api/applications")).rejects.toMatchObject({ status: 401 });

    expect(authStore.getUser()).toBeNull();
    expect(storage.getItem("aa_user")).toBeNull();
  });

  it("does not sign out when the refresh was only rate limited", async () => {
    const { apiFetch, authStore } = await load();
    authStore.updateUser(USER as never);
    fetchMock.mockResolvedValueOnce(json(401)).mockResolvedValueOnce(json(429));

    await expect(apiFetch("/api/applications")).rejects.toMatchObject({ status: 401 });

    expect(authStore.getUser()).not.toBeNull();
  });
});
