import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";

// The page itself is a client component, so its metadata has to live here.
export async function generateMetadata({ params }: LayoutProps<"/[locale]/login">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/login", "login");
}

export default function LoginLayout({ children }: LayoutProps<"/[locale]/login">) {
  return children;
}
