import type { Metadata } from "next";
import { setRequestLocale } from "next-intl/server";
import { BlogPreview } from "@/components/blog/BlogPreview";

/**
 * The editor's preview of one post (2026-09-20): the draft, rendered by the public page's own
 * `BlogArticle` inside the public chrome, so what the admin sees is what publishing would put
 * on the site. Lives under the public layout for exactly that reason — the signed-in layout has
 * a different frame. Client-rendered: the draft is the author's alone and the token that proves
 * it lives in the browser. Never indexed; there is nothing on the server-rendered page anyway.
 */
export const metadata: Metadata = { robots: { index: false, follow: false } };

export default async function BlogPreviewPage({ params }: PageProps<"/[locale]/blog/preview/[id]">) {
  const { locale, id } = await params;
  setRequestLocale(locale);
  return <BlogPreview postId={id} />;
}
