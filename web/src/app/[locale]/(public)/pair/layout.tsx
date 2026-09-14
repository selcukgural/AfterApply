import { Suspense } from "react";
import { setRequestLocale } from "next-intl/server";
import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";

// The page itself is a client component, so its metadata has to live here.
//
// noindex, like the password reset page and the OAuth callbacks: the URL only means anything while
// one specific pairing request is open, so an indexed copy would be a dead link pointing at an
// expired code. It is also, deliberately, not in PUBLIC_PATHS/the sitemap — but it *is* in the
// visit counter's allowlist, because "how many people got as far as opening this" is one of the
// few numbers that says whether the extension funnel works at all.
export async function generateMetadata({ params }: LayoutProps<"/[locale]/pair">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/pair", "pair", { index: false });
}

export default async function PairLayout({ children, params }: LayoutProps<"/[locale]/pair">) {
  const { locale } = await params;
  setRequestLocale(locale);
  // The page reads the URL's query string with useSearchParams(), which needs a Suspense boundary
  // above it for the static shell to prerender; the page fills in once the browser has the URL.
  return <Suspense>{children}</Suspense>;
}
