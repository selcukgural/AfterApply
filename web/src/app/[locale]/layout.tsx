import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { NextIntlClientProvider } from "next-intl";
import { getMessages, getTranslations, setRequestLocale } from "next-intl/server";
import { hasLocale } from "next-intl";
import { notFound } from "next/navigation";
import "../globals.css";
import { routing } from "@/i18n/routing";
import { QueryProvider } from "@/lib/api/QueryProvider";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { ROOT_MESSAGE_SCOPE, pickMessages } from "@/lib/i18n/messageScopes";
import { THEME_BOOT_SCRIPT } from "@/lib/theme/theme";
import { PreconnectApi } from "@/components/layout/PreconnectApi";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export function generateStaticParams() {
  return routing.locales.map((locale) => ({ locale }));
}

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("metadata");
  return {
    metadataBase: new URL("https://ekariyerim.com"),
    title: t("title"),
    description: t("description"),
  };
}

/**
 * Two things about this layout are the reason the public pages prerender statically instead of
 * being rendered on every request (2026-09-14):
 *
 * 1. **The theme is not read from the cookie here.** `cookies()` in a root layout opts every route
 *    under it out of static rendering, which is what put `cache-control: no-store` on the landing
 *    page and every help article, and left each first visit waiting on a Cloud Run cold start. The
 *    inline script in `<head>` reads the same cookie in the browser and stamps the `dark` class
 *    before first paint, so there is still no flash; `suppressHydrationWarning` on `<html>` is what
 *    lets React accept the class the script added. Next's own guide recommends exactly this.
 * 2. **Only the shared chrome's messages are provided here.** See messageScopes.ts — the nested
 *    layouts add what their pages need, the signed-in one the whole catalogue.
 */
export default async function LocaleLayout({ children, params }: LayoutProps<"/[locale]">) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) {
    notFound();
  }
  setRequestLocale(locale);
  const messages = pickMessages(await getMessages(), ROOT_MESSAGE_SCOPE);

  return (
    <html lang={locale} className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`} suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: THEME_BOOT_SCRIPT }} />
      </head>
      <body className="min-h-screen flex flex-col bg-gray-50 dark:bg-gray-950">
        <PreconnectApi />
        <NextIntlClientProvider messages={messages}>
          <QueryProvider>
            <AuthProvider>{children}</AuthProvider>
          </QueryProvider>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
