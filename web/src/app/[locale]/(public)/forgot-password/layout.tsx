import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";

// The page itself is a client component, so its metadata has to live here.
export async function generateMetadata({ params }: LayoutProps<"/[locale]/forgot-password">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/forgot-password", "forgotPassword", { index: false });
}

export default function ForgotPasswordLayout({ children }: LayoutProps<"/[locale]/forgot-password">) {
  return children;
}
