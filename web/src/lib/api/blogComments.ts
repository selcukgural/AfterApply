import type {
  AdminBlogComment,
  AdminBlogCommentListItem,
  BlogComment,
  BlogCommentHelpfulResponse,
  BlogCommentList,
  BlogCommentReportResponse,
  BlogCommentStatus,
  CreateBlogCommentRequest,
  MyBlogComment,
  PagedResult,
  ReportBlogCommentRequest,
} from "@/types/api";
import type { VoteSnapshot } from "@/lib/blog/vote";
import { apiFetch } from "./httpClient";

/**
 * Reader comments on blog posts (2026-09-20). The list is read through `apiFetch` on purpose,
 * unlike the post itself: with a token the API adds the viewer's own pending comments and their
 * helpful votes, so the page must ask as who it is. The anonymous first render comes from
 * `lib/blog/publicApi.server.ts` instead.
 */
export const blogCommentsApi = {
  list: (postId: string, page = 1) =>
    apiFetch<BlogCommentList>(`/api/blog/public/posts/${postId}/comments${page > 1 ? `?page=${page}` : ""}`),

  create: (postId: string, request: CreateBlogCommentRequest) =>
    apiFetch<BlogComment>(`/api/blog/posts/${postId}/comments`, { method: "POST", body: JSON.stringify(request) }),

  reply: (commentId: string, request: CreateBlogCommentRequest) =>
    apiFetch<BlogComment>(`/api/blog/comments/${commentId}/replies`, { method: "POST", body: JSON.stringify(request) }),

  /** The author's edit while the comment waits; a 409 means it no longer does. */
  edit: (commentId: string, request: CreateBlogCommentRequest) =>
    apiFetch<BlogComment>(`/api/blog/comments/${commentId}`, { method: "PUT", body: JSON.stringify(request) }),

  toggleHelpful: (commentId: string): Promise<VoteSnapshot> =>
    apiFetch<BlogCommentHelpfulResponse>(`/api/blog/comments/${commentId}/helpful`, { method: "POST" }).then((response) => ({
      on: response.helpful,
      count: response.helpfulCount,
    })),

  report: (commentId: string, request: ReportBlogCommentRequest) =>
    apiFetch<BlogCommentReportResponse>(`/api/blog/comments/${commentId}/reports`, { method: "POST", body: JSON.stringify(request) }),

  /** The caller's own comments, every status, newest first — the contributions page. */
  mine: (page = 1) => apiFetch<PagedResult<MyBlogComment>>(`/api/blog/comments/mine${page > 1 ? `?page=${page}` : ""}`),
};

export interface AdminBlogCommentFilters {
  status?: BlogCommentStatus;
  reported?: boolean;
  page?: number;
}

export const adminBlogCommentsApi = {
  list: (filters: AdminBlogCommentFilters = {}) => {
    const params = new URLSearchParams();
    if (filters.status) params.set("status", filters.status);
    if (filters.reported) params.set("reported", "true");
    if (filters.page && filters.page > 1) params.set("page", String(filters.page));
    const query = params.toString();
    return apiFetch<PagedResult<AdminBlogCommentListItem>>(`/api/admin/blog/comments${query ? `?${query}` : ""}`);
  },

  get: (commentId: string) => apiFetch<AdminBlogComment>(`/api/admin/blog/comments/${commentId}`),

  approve: (commentId: string) => apiFetch<AdminBlogComment>(`/api/admin/blog/comments/${commentId}/approve`, { method: "POST" }),

  reject: (commentId: string) => apiFetch<AdminBlogComment>(`/api/admin/blog/comments/${commentId}/reject`, { method: "POST" }),

  dismissReports: (commentId: string) =>
    apiFetch<AdminBlogComment>(`/api/admin/blog/comments/${commentId}/reports/dismiss`, { method: "POST" }),
};
