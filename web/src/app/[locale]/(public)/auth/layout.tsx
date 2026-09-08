import type { Metadata } from "next";

// OAuth callbacks are single-use, per-request pages reached only from Google/LinkedIn. There
// is nothing here for a search result to point at, and the URL carries a one-shot code.
export const metadata: Metadata = {
  robots: { index: false, follow: false },
};

export default function AuthCallbackLayout({ children }: LayoutProps<"/[locale]/auth">) {
  return children;
}
