import { AdminPostsPage } from "@/components/blog/admin/AdminPostsPage";

/**
 * The guide's admin table (2026-09-26, canvas variant B: a tab of its own). The guide is written
 * in the blog's editor and read through the blog's API with `kind=Guide`; only the section differs.
 */
export default function AdminGuidePage() {
  return <AdminPostsPage kind="Guide" />;
}
