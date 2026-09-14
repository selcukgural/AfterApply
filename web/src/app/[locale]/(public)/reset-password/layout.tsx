import { Suspense } from "react";
import { setRequestLocale } from "next-intl/server";
import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";

// The page itself is a client component, so its metadata has to live here.
export async function generateMetadata({ params }: LayoutProps<"/[locale]/reset-password">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/reset-password", "resetPassword", { index: false });
}

export default async function ResetPasswordLayout({ children, params }: LayoutProps<"/[locale]/reset-password">) {
  const { locale } = await params;
  setRequestLocale(locale);
  // The page reads the URL's query string with useSearchParams(), which needs a Suspense boundary
  // above it for the static shell to prerender; the page fills in once the browser has the URL.
  return <Suspense>{children}</Suspense>;
}
