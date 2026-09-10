import { describe, expect, it } from "vitest";
import type { UserProfileResponse } from "@/types/api";
import { ADMIN_NAV_HREF, canSeeAdminNav } from "./adminNav";

function profile(overrides: Partial<UserProfileResponse> = {}): UserProfileResponse {
  return {
    id: "1e7d4b0c-0000-4000-8000-000000000001",
    email: "someone@example.com",
    firstName: "Ada",
    lastName: "Lovelace",
    createdAt: "2026-09-01T00:00:00Z",
    consentAcceptedAt: "2026-09-01T00:00:00Z",
    preferredLanguage: "tr",
    preferredTheme: "light",
    hasPassword: true,
    isAdmin: false,
    ...overrides,
  };
}

describe("the admin nav link", () => {
  it("stays hidden while nobody is signed in", () => {
    expect(canSeeAdminNav(null)).toBe(false);
  });

  it("stays hidden for an ordinary account", () => {
    expect(canSeeAdminNav(profile())).toBe(false);
  });

  it("shows for an account the server flagged as admin", () => {
    expect(canSeeAdminNav(profile({ isAdmin: true }))).toBe(true);
  });

  /** A profile cached in localStorage before this field shipped has no `isAdmin` key. Reading an
   *  absent field as "admin" would be the wrong way round to fail. */
  it("stays hidden for a profile stored before the field existed", () => {
    const legacy = profile();
    delete (legacy as Partial<UserProfileResponse>).isAdmin;

    expect(canSeeAdminNav(legacy)).toBe(false);
  });

  /** The stored profile is JSON on the user's own disk; it can hold anything by the time it is read
   *  back. Only the boolean the API actually sends counts. */
  it("ignores a non-boolean value in the stored profile", () => {
    expect(canSeeAdminNav({ ...profile(), isAdmin: "true" } as unknown as UserProfileResponse)).toBe(false);
    expect(canSeeAdminNav({ ...profile(), isAdmin: 1 } as unknown as UserProfileResponse)).toBe(false);
  });

  it("points at the one admin page there is", () => {
    expect(ADMIN_NAV_HREF).toBe("/admin/metrics");
  });
});
