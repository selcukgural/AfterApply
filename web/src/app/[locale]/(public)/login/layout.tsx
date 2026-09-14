import { setRequestLocale } from "next-intl/server";
import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";

// The page itself is a client component, so its metadata has to live here.
export async function generateMetadata({ params }: LayoutProps<"/[locale]/login">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/login", "login");
}

export default async function LoginLayout({ children, params }: LayoutProps<"/[locale]/login">) {
  const { locale } = await params;
  setRequestLocale(locale);
  return children;
}
