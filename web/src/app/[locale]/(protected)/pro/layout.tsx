import { setRequestLocale } from "next-intl/server";
import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";

// The pages under /pro are client components, so their metadata lives here. noindex: the plan
// page is behind sign-in and the checkout/result pages mean nothing outside one session.
export async function generateMetadata({ params }: LayoutProps<"/[locale]/pro">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/pro", "pro", { index: false });
}

export default async function ProLayout({ children, params }: LayoutProps<"/[locale]/pro">) {
  const { locale } = await params;
  setRequestLocale(locale);
  return children;
}
