import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";

// The page itself is a client component, so its metadata has to live here.
export async function generateMetadata({ params }: LayoutProps<"/[locale]/register">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/register", "register");
}

export default function RegisterLayout({ children }: LayoutProps<"/[locale]/register">) {
  return children;
}
