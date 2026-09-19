"use client";

import dynamic from "next/dynamic";
import { useParams } from "next/navigation";

// ProseMirror touches `document` when it loads, so the editor is a browser-only chunk: nothing
// of it is rendered on the server, and nothing of it reaches any other page's bundle.
const BlogEditorPage = dynamic(() => import("@/components/blog/editor/BlogEditorPage").then((m) => m.BlogEditorPage), {
  ssr: false,
  loading: () => <p className="text-sm text-gray-500 dark:text-gray-400">…</p>,
});

export default function AdminBlogEditorRoute() {
  const { id } = useParams<{ id: string }>();
  return <BlogEditorPage postId={id} />;
}
