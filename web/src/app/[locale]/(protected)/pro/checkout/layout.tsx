import { Suspense } from "react";
import { setRequestLocale } from "next-intl/server";
import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";

export async function generateMetadata({ params }: LayoutProps<"/[locale]/pro/checkout">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/pro/checkout", "proCheckout", { index: false });
}

export default async function CheckoutLayout({ children, params }: LayoutProps<"/[locale]/pro/checkout">) {
  const { locale } = await params;
  setRequestLocale(locale);
  // The page reads ?plan= with useSearchParams(), which needs a Suspense boundary above it.
  return <Suspense>{children}</Suspense>;
}
