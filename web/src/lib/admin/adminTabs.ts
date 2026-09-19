/**
 * Which top-level admin tab the current page belongs to. Exact match, with one seam: the
 * "Reviews" tab is the umbrella for the three contribution tables (2026-09-18), so
 * /admin/reviews/salaries and /admin/reviews/experiences light it up — but /admin/reviews/reports
 * is its own tab and must not.
 */
export function isAdminTabActive(pathname: string, href: string): boolean {
  if (pathname === href) return true;
  // The blog tab stays lit inside an editor (/admin/blog/{id}), the way the reviews tab does on
  // its sibling tables.
  if (href === "/admin/blog") return pathname.startsWith("/admin/blog/");
  return href === "/admin/reviews" && (pathname === "/admin/reviews/salaries" || pathname === "/admin/reviews/experiences");
}

export const ADMIN_CONTRIBUTION_TABS = [
  { href: "/admin/reviews", key: "reviews" },
  { href: "/admin/reviews/salaries", key: "salaries" },
  { href: "/admin/reviews/experiences", key: "experiences" },
] as const;
