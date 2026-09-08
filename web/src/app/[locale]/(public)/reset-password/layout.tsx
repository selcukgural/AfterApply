import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";

// The page itself is a client component, so its metadata has to live here.
export async function generateMetadata({ params }: LayoutProps<"/[locale]/reset-password">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/reset-password", "resetPassword", { index: false });
}

export default function ResetPasswordLayout({ children }: LayoutProps<"/[locale]/reset-password">) {
  return children;
}
