import { Suspense } from "react";
import { setRequestLocale } from "next-intl/server";
import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";

export async function generateMetadata({ params }: LayoutProps<"/[locale]/pro/orders/[orderId]">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/pro", "proOrder", { index: false });
}

export default async function OrderResultLayout({ children, params }: LayoutProps<"/[locale]/pro/orders/[orderId]">) {
  const { locale } = await params;
  setRequestLocale(locale);
  // The page reads ?outcome= with useSearchParams(), which needs a Suspense boundary above it.
  return <Suspense>{children}</Suspense>;
}
