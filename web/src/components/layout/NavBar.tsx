"use client";

import { type ReactNode, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { ADMIN_NAV_HREF, canSeeAdminNav } from "@/lib/auth/adminNav";
import { PRO_NAV_HREF, canSeeProNav } from "@/lib/payments/proNav";
import { useSuggestionCount } from "@/hooks/useSuggestionCount";
import { useNotificationCount } from "@/hooks/useNotificationCount";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Logo } from "@/components/layout/Logo";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import { displayName } from "@/lib/auth/displayName";
import { NotificationBell } from "@/components/notifications/NotificationBell";
import { UserMenu } from "@/components/layout/UserMenu";
import { NavMenu, type NavMenuItem } from "@/components/layout/NavMenu";
import { ProBadge } from "@/components/layout/ProBadge";
import { isActivePath, navLinkClassName } from "@/components/layout/navLink";
import { buildNavEntries, isNavItemActive, type NavItem } from "@/components/layout/navGroups";
import { useClientConfig } from "@/hooks/useClientConfig";
import { useProBadge } from "@/hooks/useProBadge";

/**
 * The signed-in app's header. Since 2026-09-14 it is also what a signed-in visitor gets on the
 * public pages (SiteHeader hands over to it), so /companies, the guide and the help centre no
 * longer drop the app's menu and avatar — which read as "I have been signed out".
 *
 * The row and the mobile drawer are both drawn from `buildNavEntries` (navGroups.ts), in the
 * same order with the same headings: Dashboard, an Applications group, CVs, a Companies group
 * (browse → contribute → mine), a Tools group — then "New application" as the one primary
 * button, suggestions and notifications as icon buttons, and the avatar menu for account matters
 * (2026-09-17, variant A on the navigation canvas). The 2026-09-15 row had folded the discovery
 * pages into one "Explore" list and the drawer had grown its own, different grouping.
 */
export function NavBar() {
  const { user, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const t = useTranslations("nav");
  const locale = useLocale();
  const { data: suggestionCount } = useSuggestionCount();
  const { data: notificationCount } = useNotificationCount();
  const { config } = useClientConfig();
  const { showProBadge } = useProBadge();
  const [menuOpen, setMenuOpen] = useState(false);

  const entries = buildNavEntries(config, locale);

  const handleLogout = async () => {
    await logout();
    router.replace("/login");
  };

  // The flag rides on the profile, which is re-fetched on every mount (AuthContext validates the
  // stored session against /api/users/me), so a grant or a revoke reaches the menu on the next page
  // load rather than at the next sign-in.
  const showAdmin = canSeeAdminNav(user);
  const showPro = canSeeProNav(config);

  // An account may have no name at all (sign-up stopped asking on 2026-09-14): the header then
  // shows the part of the e-mail before the @, and the avatar its first letter.
  const { name: fullName, initials } = displayName(user);

  const active = (href: string) => isActivePath(pathname, href);
  const desktopLink = (href: string) => navLinkClassName("underline", active(href), "flex items-center gap-1.5 whitespace-nowrap pb-0.5");
  const mobileLink = (href: string, isActive = active(href)) => navLinkClassName("pill", isActive, "flex items-center gap-1.5 px-3 py-2");

  const itemLabel = (item: NavItem): ReactNode =>
    item.proBadge && showProBadge ? (
      <>
        {t(item.key)}
        <ProBadge />
      </>
    ) : (
      t(item.key)
    );

  const menuItems = (items: NavItem[]): NavMenuItem[] =>
    items.map((item) => ({ href: item.href, label: itemLabel(item), dividerBefore: item.dividerBefore }));

  const badge = (count: number | undefined) =>
    count ? (
      <span className="inline-flex min-w-[1.25rem] items-center justify-center rounded-full bg-blue-600 px-1.5 py-0.5 text-xs font-semibold leading-none text-white">
        {count}
      </span>
    ) : null;

  // The two "something is waiting for you" destinations, as icons with their counts by the
  // avatar: they are signals, not sections, and the count is what a glance is after.
  const iconLink = (href: string, label: string, count: number | undefined, icon: ReactNode) => (
    <Link
      href={href}
      aria-label={count ? `${label} (${count})` : label}
      title={label}
      aria-current={active(href) ? "page" : undefined}
      className={`relative flex h-9 w-9 items-center justify-center rounded-md transition-colors ${
        active(href)
          ? "bg-gray-100 text-gray-900 dark:bg-gray-800 dark:text-gray-100"
          : "text-gray-600 hover:bg-gray-100 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-800 dark:hover:text-gray-100"
      }`}
    >
      {icon}
      {count ? <span className="absolute -top-1 -right-1">{badge(count)}</span> : null}
    </Link>
  );

  const inboxIcon = (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M22 12h-6l-2 3h-4l-2-3H2" />
      <path d="M5.5 5h13l3.5 7v7a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1v-7z" />
    </svg>
  );
  const bellIcon = (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" />
      <path d="M10 21h4" />
    </svg>
  );

  // The app's one primary action, on every page rather than only on the dashboard. Hidden on the
  // form itself.
  const newApplicationButton = (mobile: boolean) =>
    pathname === "/applications/new" ? null : (
      <Link
        href="/applications/new"
        onClick={mobile ? () => setMenuOpen(false) : undefined}
        className={buttonClassName("primary", mobile ? "block text-center" : "whitespace-nowrap px-3 py-1.5")}
      >
        {t("newApplication")}
      </Link>
    );

  const mobileHeading = (label: string) => (
    <p className="mb-1 px-3 text-xs font-semibold tracking-wide text-gray-500 uppercase dark:text-gray-400">{label}</p>
  );

  return (
    <header className="border-b border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
      {/* max-w-6xl, the public header's width, not the app content's max-w-5xl: the row with its
          groups and the primary button is wider than the app's content column, and at 5xl the
          logo wrapped onto two lines. A header wider than its page is what the public pages do. */}
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
        <div className="flex items-center gap-6">
          <Link href="/dashboard">
            <Logo />
          </Link>
          <nav className="hidden items-center gap-4 text-sm md:flex">
            {entries.map((entry) =>
              entry.type === "link" ? (
                <Link key={entry.href} href={entry.href} aria-current={active(entry.href) ? "page" : undefined} className={desktopLink(entry.href)}>
                  {t(entry.key)}
                </Link>
              ) : (
                <NavMenu key={entry.key} label={t(entry.key)} items={menuItems(entry.items)} />
              ),
            )}
          </nav>
        </div>

        <div className="hidden items-center gap-1 md:flex">
          <div className="mr-2">{newApplicationButton(false)}</div>
          {iconLink("/suggestions", t("suggestions"), suggestionCount, inboxIcon)}
          <NotificationBell badge={badge} icon={bellIcon} />
          {user && (
            <div className="ml-2">
              <UserMenu
                name={fullName}
                initials={initials}
                onLogout={handleLogout}
                showAdmin={showAdmin}
                showPro={showPro}
              />
            </div>
          )}
        </div>

        <button
          type="button"
          onClick={() => setMenuOpen((open) => !open)}
          aria-expanded={menuOpen}
          aria-controls="app-mobile-menu"
          aria-label={menuOpen ? t("closeMenu") : t("openMenu")}
          className="flex h-9 w-9 items-center justify-center rounded-md text-gray-700 hover:bg-gray-100 md:hidden dark:text-gray-300 dark:hover:bg-gray-800"
        >
          {menuOpen ? (
            <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          ) : (
            <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
              <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5M3.75 17.25h16.5" />
            </svg>
          )}
        </button>
      </div>

      {menuOpen && (
        <div id="app-mobile-menu" className="border-t border-gray-200 px-4 py-4 md:hidden dark:border-gray-800">
          {/* The row, read top to bottom: a group becomes a heading over its items. */}
          <nav className="flex flex-col gap-1 text-sm">
            {entries.map((entry, index) =>
              entry.type === "link" ? (
                <div key={entry.href} className={index === 0 ? "flex flex-col gap-2" : "mt-3 border-t border-gray-100 pt-3 dark:border-gray-800"}>
                  <Link href={entry.href} onClick={() => setMenuOpen(false)} aria-current={active(entry.href) ? "page" : undefined} className={mobileLink(entry.href)}>
                    {t(entry.key)}
                  </Link>
                  {index === 0 && newApplicationButton(true)}
                </div>
              ) : (
                <div key={entry.key} className="mt-3 border-t border-gray-100 pt-3 dark:border-gray-800">
                  {mobileHeading(t(entry.key))}
                  {entry.items.map((item) => (
                    <Link
                      key={item.href}
                      href={item.href}
                      onClick={() => setMenuOpen(false)}
                      aria-current={isNavItemActive(pathname, item.href) ? "page" : undefined}
                      className={mobileLink(item.href, isNavItemActive(pathname, item.href))}
                    >
                      {itemLabel(item)}
                    </Link>
                  ))}
                </div>
              ),
            )}
          </nav>

          <nav className="mt-3 flex flex-col gap-1 border-t border-gray-100 pt-3 text-sm dark:border-gray-800">
            <Link href="/suggestions" onClick={() => setMenuOpen(false)} aria-current={active("/suggestions") ? "page" : undefined} className={mobileLink("/suggestions")}>
              {t("suggestions")}
              {badge(suggestionCount)}
            </Link>
            <Link href="/notifications" onClick={() => setMenuOpen(false)} aria-current={active("/notifications") ? "page" : undefined} className={mobileLink("/notifications")}>
              {t("notifications")}
              {badge(notificationCount)}
            </Link>
          </nav>

          {/* The same items, in the same order, as the avatar menu on desktop (UserMenu). */}
          <div className="mt-3 border-t border-gray-100 pt-3 dark:border-gray-800">
            {mobileHeading(user ? fullName : t("account"))}
            <nav className="flex flex-col gap-1 text-sm">
              <Link href="/profile" onClick={() => setMenuOpen(false)} className={mobileLink("/profile")}>
                {t("profile")}
              </Link>
              <Link href="/settings" onClick={() => setMenuOpen(false)} className={mobileLink("/settings")}>
                {t("accountSettings")}
              </Link>
              {showPro && (
                <Link href={PRO_NAV_HREF} onClick={() => setMenuOpen(false)} className={mobileLink("/pro")}>
                  {t("pro")}
                </Link>
              )}
              {showAdmin && (
                <Link href={ADMIN_NAV_HREF} onClick={() => setMenuOpen(false)} className={mobileLink("/admin")}>
                  {t("admin")}
                </Link>
              )}
              <Link href="/help" onClick={() => setMenuOpen(false)} className={mobileLink("/help")}>
                {t("help")}
              </Link>
            </nav>
          </div>

          <div className="mt-4 flex items-center gap-3">
            <LanguageSwitcher />
            <ThemeSwitcher />
          </div>

          <div className="mt-4">
            <Button variant="secondary" onClick={handleLogout}>
              {t("logout")}
            </Button>
          </div>
        </div>
      )}
    </header>
  );
}
