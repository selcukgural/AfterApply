import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { NextIntlClientProvider } from "next-intl";
import { getMessages, getTranslations, setRequestLocale } from "next-intl/server";
import { hasLocale } from "next-intl";
import { headers } from "next/headers";
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
 * Every page under this layout is rendered per request (since 2026-09-27): the CSP carries a fresh
 * nonce on each response and only a script that repeats it may run (proxy.ts,
 * lib/http/contentSecurityPolicy.ts). Next.js stamps the nonce on its own scripts; the theme boot
 * script below gets it here, which is also what makes the layout dynamic — a prerendered page
 * could not carry a per-response nonce.
 *
 * - **The theme is still applied by the inline script**, not read from the cookie on the server:
 *   it stamps the `dark` class before first paint, so there is no flash, and
 *   `suppressHydrationWarning` on `<html>` lets React accept the class the script added.
 * - **Only the shared chrome's messages are provided here.** See messageScopes.ts — the nested
 *   layouts add what their pages need, the signed-in one the whole catalogue.
 */
export default async function LocaleLayout({ children, params }: LayoutProps<"/[locale]">) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) {
    notFound();
  }
  setRequestLocale(locale);
  const messages = pickMessages(await getMessages(), ROOT_MESSAGE_SCOPE);
  const nonce = (await headers()).get("x-nonce") ?? undefined;

  return (
    <html lang={locale} className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`} suppressHydrationWarning>
      <head>
        <script nonce={nonce} dangerouslySetInnerHTML={{ __html: THEME_BOOT_SCRIPT }} />
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
