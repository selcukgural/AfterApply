import { Suspense } from "react";
import { setRequestLocale } from "next-intl/server";
import type { Metadata } from "next";

// OAuth callbacks are single-use, per-request pages reached only from Google/LinkedIn. There
// is nothing here for a search result to point at, and the URL carries a one-shot code.
export const metadata: Metadata = {
  robots: { index: false, follow: false },
};

export default async function AuthCallbackLayout({ children, params }: LayoutProps<"/[locale]/auth">) {
  const { locale } = await params;
  setRequestLocale(locale);
  // The page reads the URL's query string with useSearchParams(), which needs a Suspense boundary
  // above it for the static shell to prerender; the page fills in once the browser has the URL.
  return <Suspense>{children}</Suspense>;
}
