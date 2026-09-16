import { Suspense } from "react";
import { setRequestLocale } from "next-intl/server";
import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";

// Outside the (protected) and (public) route groups on purpose: this page carries no chrome and
// no auth guard, because it may be rendered inside PayTR's payment frame (see page.tsx) where
// there is nothing to show but a redirect. The CSP for this path alone allows paytr.com as a
// frame ancestor (next.config.ts).
export async function generateMetadata({ params }: LayoutProps<"/[locale]/pro/return/[orderId]">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/pro", "proOrder", { index: false });
}

export default async function ReturnLayout({ children, params }: LayoutProps<"/[locale]/pro/return/[orderId]">) {
  const { locale } = await params;
  setRequestLocale(locale);
  return <Suspense>{children}</Suspense>;
}
