import type { Metadata } from "next";
import { setRequestLocale } from "next-intl/server";
import { BlogPreview } from "@/components/blog/BlogPreview";

/**
 * The editor's preview of one guide (2026-09-26) — the blog preview's route under the guide's
 * section, so the draft is shown inside the same chrome and with the same component
 * (`GuideArticle`) as the live page. Client-rendered for the same reason (the token is in the
 * browser), and never indexed.
 */
export const metadata: Metadata = { robots: { index: false, follow: false } };

export default async function GuidePreviewPage({ params }: PageProps<"/[locale]/guide/preview/[id]">) {
  const { locale, id } = await params;
  setRequestLocale(locale);
  return <BlogPreview postId={id} kind="Guide" />;
}
