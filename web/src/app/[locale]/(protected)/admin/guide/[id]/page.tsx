"use client";

import { Suspense } from "react";
import dynamic from "next/dynamic";
import { useParams, useSearchParams } from "next/navigation";
import { newPostSeedFrom } from "@/lib/blog/newPostSeed";

// ProseMirror touches `document` when it loads, so the editor is a browser-only chunk: nothing
// of it is rendered on the server, and nothing of it reaches any other page's bundle.
const BlogEditorPage = dynamic(() => import("@/components/blog/editor/BlogEditorPage").then((m) => m.BlogEditorPage), {
  ssr: false,
  loading: () => <p className="text-sm text-gray-500 dark:text-gray-400">…</p>,
});

/**
 * The guide's editor (2026-09-26): the blog's, opened on a guide. Everything below is the blog
 * editor route's.
 *
 * `/admin/guide/new` opens the editor on a post that does not exist yet — with `?lang=&translationOf=`
 * from the admin table's "add translation" (2026-09-20) it opens on the other language, linked;
 * anything else is an id.
 */
function AdminGuideEditor() {
  const { id } = useParams<{ id: string }>();
  const searchParams = useSearchParams();
  const isNew = id === "new";
  return <BlogEditorPage kind="Guide" postId={isNew ? null : id} newPostSeed={isNew ? newPostSeedFrom(searchParams) : undefined} />;
}

export default function AdminGuideEditorRoute() {
  // `useSearchParams` needs a boundary above it on a page that may be prerendered.
  return (
    <Suspense fallback={<p className="text-sm text-gray-500 dark:text-gray-400">…</p>}>
      <AdminGuideEditor />
    </Suspense>
  );
}
