import { getServerTheme } from "@/lib/theme/getServerTheme";
import { SiteTrafficReporter } from "@/components/analytics/SiteTrafficReporter";
import { PUBLIC_SITE_LINKS, SiteHeader } from "@/components/layout/SiteHeader";
import { SiteFooter } from "@/components/layout/SiteFooter";

/**
 * The signed-out chrome: the same header and footer as the landing page (SiteHeader knows whether
 * the visitor is signed in and offers the dashboard or sign-in accordingly). Before 2026-09-13
 * this layout had a header of its own with no sign-in, no menu and no footer — see SiteHeader.
 */
export default async function PublicLayout({ children }: { children: React.ReactNode }) {
  const theme = await getServerTheme();

  return (
    <>
      <SiteTrafficReporter />
      <SiteHeader initialTheme={theme} links={PUBLIC_SITE_LINKS} />
      <main className="flex flex-1 flex-col">{children}</main>
      <SiteFooter />
    </>
  );
}
