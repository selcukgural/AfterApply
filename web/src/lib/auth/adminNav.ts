import type { UserProfileResponse } from "@/types/api";

/**
 * Where the admin link points. There is one admin page today (`/admin/metrics`); the constant is
 * here so the navigation, the tests and any future second page agree on one entry point rather
 * than repeating the path in the desktop menu and the mobile menu.
 */
export const ADMIN_NAV_HREF = "/admin/metrics";

/**
 * Whether to render the admin link for this profile.
 *
 * Strictly `=== true`, not a truthiness check, for two reasons that are both real here. The profile
 * is read back from `localStorage` (see `authStore`), so a session that predates this field
 * deserialises without `isAdmin` at all — and it may be anything at all if someone edits the stored
 * JSON by hand. Both cases must read as "no link".
 *
 * This is presentation only. The link it guards leads to a page whose every request is checked
 * against `Users.IsAdmin` server-side, so forcing this to true shows a menu item that answers 403 —
 * which is exactly why it is safe to decide in the client at all.
 */
export function canSeeAdminNav(user: UserProfileResponse | null): boolean {
  return user?.isAdmin === true;
}
