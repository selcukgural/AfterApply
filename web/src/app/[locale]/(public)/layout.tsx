import { NextIntlClientProvider } from "next-intl";
import { getMessages, setRequestLocale } from "next-intl/server";
import { SiteTrafficReporter } from "@/components/analytics/SiteTrafficReporter";
import { PUBLIC_SITE_LINKS, SiteHeader } from "@/components/layout/SiteHeader";
import { SiteFooter } from "@/components/layout/SiteFooter";
import { PUBLIC_MESSAGE_SCOPE, pickMessages } from "@/lib/i18n/messageScopes";

/**
 * The signed-out chrome: the same header and footer as the landing page (SiteHeader knows whether
 * the visitor is signed in and offers the dashboard or sign-in accordingly). Before 2026-09-13
 * this layout had a header of its own with no sign-in, no menu and no footer — see SiteHeader.
 *
 * Provides the public pages' message namespaces (messageScopes.ts) and pins the locale so the
 * pages under it can prerender statically.
 */
export default async function PublicLayout({ children, params }: LayoutProps<"/[locale]">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const messages = pickMessages(await getMessages(), PUBLIC_MESSAGE_SCOPE);

  return (
    <NextIntlClientProvider messages={messages}>
      <SiteTrafficReporter />
      <SiteHeader links={PUBLIC_SITE_LINKS} />
      <main className="flex flex-1 flex-col">{children}</main>
      <SiteFooter />
    </NextIntlClientProvider>
  );
}
