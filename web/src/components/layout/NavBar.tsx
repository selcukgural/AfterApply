"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { ADMIN_NAV_HREF, canSeeAdminNav } from "@/lib/auth/adminNav";
import { PRO_NAV_HREF, canSeeProNav } from "@/lib/payments/proNav";
import { useSuggestionCount } from "@/hooks/useSuggestionCount";
import { useNotificationCount } from "@/hooks/useNotificationCount";
import { Button } from "@/components/ui/Button";
import { Logo } from "@/components/layout/Logo";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import { displayName } from "@/lib/auth/displayName";
import { UserMenu } from "@/components/layout/UserMenu";
import { TOOL_LINKS, ToolsMenu } from "@/components/layout/ToolsMenu";
import { isActivePath, navLinkClassName } from "@/components/layout/navLink";
import { useClientConfig } from "@/hooks/useClientConfig";

const NAV_LINKS = [
  { href: "/dashboard", key: "dashboard" },
  { href: "/applications", key: "applications" },
  { href: "/tracked-jobs", key: "trackedJobs" },
  { href: "/cv", key: "cv" },
  { href: "/import", key: "import" },
  // Public page, but listed here too: a signed-in person is the one who can write a review.
  { href: "/companies", key: "companies" },
] as const;

/**
 * The signed-in app's header. Since 2026-09-14 it is also what a signed-in visitor gets on the
 * public pages (SiteHeader hands over to it), so /companies, the guide and the help centre no
 * longer drop the app's menu and avatar — which read as "I have been signed out". The
 * account-free tools sit in the "Tools" group (ToolsMenu) so that hand-over loses nothing the
 * public header offered.
 */
export function NavBar() {
  const { user, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const t = useTranslations("nav");
  const { data: suggestionCount } = useSuggestionCount();
  const { data: notificationCount } = useNotificationCount();
  const { config } = useClientConfig();
  const [menuOpen, setMenuOpen] = useState(false);

  // The paid weekly job matching ships dark (JobSources:Enabled); its link appears only when the
  // server says the routes exist, the same rule as the company pages' flag.
  const navLinks = config.jobSources?.enabled
    ? [...NAV_LINKS.slice(0, 3), { href: "/weekly-jobs", key: "weeklyJobs" } as const, ...NAV_LINKS.slice(3)]
    : NAV_LINKS;

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
  const mobileLink = (href: string) => navLinkClassName("pill", active(href), "flex items-center gap-1.5 px-3 py-2");

  const badge = (count: number | undefined) =>
    count ? (
      <span className="inline-flex min-w-[1.25rem] items-center justify-center rounded-full bg-blue-600 px-1.5 py-0.5 text-xs font-semibold leading-none text-white">
        {count}
      </span>
    ) : null;

  const suggestionsLink = (mobile: boolean) => (
    <Link
      href="/suggestions"
      onClick={mobile ? () => setMenuOpen(false) : undefined}
      aria-current={active("/suggestions") ? "page" : undefined}
      className={mobile ? mobileLink("/suggestions") : desktopLink("/suggestions")}
    >
      {t("suggestions")}
      {badge(suggestionCount)}
    </Link>
  );

  const notificationsLink = (mobile: boolean) => (
    <Link
      href="/notifications"
      onClick={mobile ? () => setMenuOpen(false) : undefined}
      aria-current={active("/notifications") ? "page" : undefined}
      className={mobile ? mobileLink("/notifications") : desktopLink("/notifications")}
    >
      {t("notifications")}
      {badge(notificationCount)}
    </Link>
  );

  return (
    <header className="border-b border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
      {/* max-w-6xl, the public header's width, not the app content's max-w-5xl: with the Tools
          menu the row is ~1080px of links and the avatar, and at 5xl the logo wrapped onto two
          lines. A header wider than its page is what the public pages already do. */}
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
        <div className="flex items-center gap-6">
          <Link href="/dashboard">
            <Logo />
          </Link>
          <nav className="hidden items-center gap-4 text-sm md:flex">
            {navLinks.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                aria-current={active(link.href) ? "page" : undefined}
                className={desktopLink(link.href)}
              >
                {t(link.key)}
              </Link>
            ))}
            {suggestionsLink(false)}
            {notificationsLink(false)}
            <ToolsMenu />
          </nav>
        </div>

        <div className="hidden md:block">
          {user && (
            <UserMenu
              name={fullName}
              initials={initials}
              onLogout={handleLogout}
              showAdmin={showAdmin}
              showPro={showPro}
            />
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
          <nav className="flex flex-col gap-1 text-sm">
            {navLinks.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                onClick={() => setMenuOpen(false)}
                aria-current={active(link.href) ? "page" : undefined}
                className={mobileLink(link.href)}
              >
                {t(link.key)}
              </Link>
            ))}
            {suggestionsLink(true)}
            {notificationsLink(true)}
          </nav>

          <div className="mt-4 border-t border-gray-100 pt-4 dark:border-gray-800">
            {user && <p className="mb-3 px-3 text-sm font-medium text-gray-900 dark:text-gray-100">{fullName}</p>}
            <nav className="flex flex-col gap-1 text-sm">
              <Link href="/help" onClick={() => setMenuOpen(false)} className={mobileLink("/help")}>
                {t("help")}
              </Link>
              <Link href="/settings" onClick={() => setMenuOpen(false)} className={mobileLink("/settings")}>
                {t("accountSettings")}
              </Link>
              <Link href="/my-reviews" onClick={() => setMenuOpen(false)} className={mobileLink("/my-reviews")}>
                {t("myReviews")}
              </Link>
              {showPro && (
                <Link href={PRO_NAV_HREF} onClick={() => setMenuOpen(false)} className={mobileLink("/pro")}>
                  {t("pro")}
                </Link>
              )}
              {/* Same group as on the desktop menu — help, settings, then admin for the accounts
                  that have it. */}
              {showAdmin && (
                <Link href={ADMIN_NAV_HREF} onClick={() => setMenuOpen(false)} className={mobileLink("/admin")}>
                  {t("admin")}
                </Link>
              )}
            </nav>
          </div>

          <div className="mt-4 border-t border-gray-100 pt-4 dark:border-gray-800">
            <p className="mb-1 px-3 text-xs font-semibold tracking-wide text-gray-500 uppercase dark:text-gray-400">{t("tools")}</p>
            <nav className="flex flex-col gap-1 text-sm">
              {TOOL_LINKS.map((link) => (
                <Link key={link.href} href={link.href} onClick={() => setMenuOpen(false)} className={mobileLink(link.href)}>
                  {t(link.key)}
                </Link>
              ))}
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
