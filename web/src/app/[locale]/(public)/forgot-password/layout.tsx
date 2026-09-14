import { setRequestLocale } from "next-intl/server";
import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";

// The page itself is a client component, so its metadata has to live here.
export async function generateMetadata({ params }: LayoutProps<"/[locale]/forgot-password">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/forgot-password", "forgotPassword", { index: false });
}

export default async function ForgotPasswordLayout({ children, params }: LayoutProps<"/[locale]/forgot-password">) {
  const { locale } = await params;
  setRequestLocale(locale);
  return children;
}
