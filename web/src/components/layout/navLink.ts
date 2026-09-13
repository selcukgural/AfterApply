/**
 * The one answer to "which link is the page I am on". Three surfaces used to decide this on their
 * own — the admin tabs with an underline, the help sidebar with a filled pill, the app navbar not
 * at all — so the same state looked like three different things, or like nothing.
 *
 * `underline` is for a horizontal row of links (the app navbar, the admin tabs); `pill` for a
 * vertical list (mobile menus, the help sidebar), where a bottom border reads as a divider.
 */
export type NavLinkVariant = "underline" | "pill";

const VARIANTS: Record<NavLinkVariant, { base: string; active: string; inactive: string }> = {
  underline: {
    base: "border-b-2 transition-colors",
    active: "border-accent font-medium text-gray-900 dark:text-gray-100",
    inactive: "border-transparent text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100",
  },
  pill: {
    base: "rounded-md transition-colors",
    active: "bg-blue-50 font-medium text-blue-700 dark:bg-blue-950/50 dark:text-blue-300",
    inactive: "text-gray-600 hover:bg-gray-100 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-800 dark:hover:text-gray-100",
  },
};

export function navLinkClassName(variant: NavLinkVariant, active: boolean, className = ""): string {
  const v = VARIANTS[variant];
  return `${v.base} ${active ? v.active : v.inactive} ${className}`.trim();
}

/** A section link is active on its own page and on every page beneath it (/applications/123). */
export function isActivePath(pathname: string, href: string): boolean {
  return pathname === href || pathname.startsWith(`${href}/`);
}
