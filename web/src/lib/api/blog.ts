import type {
  AdminBlogPost,
  AdminBlogPostGroup,
  AdminBlogPostListItem,
  BlogDraftSaved,
  BlogLanguage,
  BlogLikeToggleResponse,
  BlogMediaResponse,
  BlogPostPublic,
  BlogPostStatus,
  CreateBlogPostRequest,
  PagedResult,
  SaveBlogDraftRequest,
} from "@/types/api";
import { apiFetch, apiFetchBlob } from "./httpClient";

/** The one thing a signed-in reader does on a post. Public reads are server-side only
 *  (`lib/blog/publicApi.server.ts`), so nothing here is anonymous. */
export const blogApi = {
  toggleLike: (postId: string) => apiFetch<BlogLikeToggleResponse>(`/api/blog/posts/${postId}/like`, { method: "POST" }),

  /**
   * An image's bytes, with the caller's token. An `<img>` cannot send a Bearer, and a draft's
   * images are 404 to anyone but the author — so the editor renders them from a blob URL fetched
   * here, while the stored HTML keeps the plain relative path the public page will use.
   */
  fetchMediaBlob: (mediaUrl: string) => apiFetchBlob(mediaUrl),
};

export interface AdminBlogListFilters {
  status?: BlogPostStatus;
  lang?: BlogLanguage;
  page?: number;
}

export const adminBlogApi = {
  /** The caller's drafts and every published post, most recently touched first. */
  list: (filters: AdminBlogListFilters = {}) => {
    const params = new URLSearchParams();
    if (filters.status) params.set("status", filters.status);
    if (filters.lang) params.set("lang", filters.lang);
    if (filters.page && filters.page > 1) params.set("page", String(filters.page));
    const query = params.toString();
    return apiFetch<PagedResult<AdminBlogPostListItem>>(`/api/admin/blog/posts${query ? `?${query}` : ""}`);
  },

  /**
   * The admin table: one row per post and its translation, most recently touched pair first,
   * paged by pair. The flat `list` stays for the editor's translation picker.
   */
  listGrouped: (filters: { status?: BlogPostStatus; page?: number } = {}) => {
    const params = new URLSearchParams();
    if (filters.status) params.set("status", filters.status);
    if (filters.page && filters.page > 1) params.set("page", String(filters.page));
    const query = params.toString();
    return apiFetch<PagedResult<AdminBlogPostGroup>>(`/api/admin/blog/posts/grouped${query ? `?${query}` : ""}`);
  },

  /** The first save of a new post — the editor calls this instead of `saveDraft` until it has an id. */
  create: (request: CreateBlogPostRequest) =>
    apiFetch<AdminBlogPost>("/api/admin/blog/posts", { method: "POST", body: JSON.stringify(request) }),

  get: (postId: string) => apiFetch<AdminBlogPost>(`/api/admin/blog/posts/${postId}`),

  /** The draft in the public post's shape — what `BlogArticle` renders on the preview page. */
  preview: (postId: string) => apiFetch<BlogPostPublic>(`/api/admin/blog/posts/${postId}/preview`),

  /** The autosave. A 409 means another tab saved since — reload, do not retry. */
  saveDraft: (postId: string, request: SaveBlogDraftRequest) =>
    apiFetch<BlogDraftSaved>(`/api/admin/blog/posts/${postId}/draft`, { method: "PUT", body: JSON.stringify(request) }),

  publish: (postId: string) => apiFetch<AdminBlogPost>(`/api/admin/blog/posts/${postId}/publish`, { method: "POST" }),

  unpublish: (postId: string) => apiFetch<AdminBlogPost>(`/api/admin/blog/posts/${postId}/unpublish`, { method: "POST" }),

  remove: (postId: string) => apiFetch<void>(`/api/admin/blog/posts/${postId}`, { method: "DELETE" }),

  uploadMedia: (postId: string, file: File) => {
    const body = new FormData();
    body.append("file", file);
    // No Content-Type header: the browser sets multipart/form-data with its own boundary (see
    // performFetch, which skips the JSON default for FormData bodies).
    return apiFetch<BlogMediaResponse>(`/api/admin/blog/posts/${postId}/media`, { method: "POST", body });
  },
};
