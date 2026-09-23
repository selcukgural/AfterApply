import type {
  NotificationCountResponse,
  NotificationFeedItemResponse,
  NotificationPreferences,
  PagedResult,
} from "@/types/api";
import { apiFetch } from "./httpClient";

/** How many rows the Notifications page shows at once, and so the page size it asks for. */
export const NOTIFICATION_PAGE_SIZE = 10;

/** How many rows the bell's panel shows; the rest are one click away on the page. */
export const NOTIFICATION_PANEL_SIZE = 8;

/**
 * The bell (DECISIONS.md 2026-09-23): contribution notifications and the Gmail scan's status
 * changes as one list. The Gmail-only `/api/email-forwarding/notifications*` routes still exist;
 * the web reads this one.
 */
export const notificationsApi = {
  getNotifications: (page: number, pageSize = NOTIFICATION_PAGE_SIZE) =>
    apiFetch<PagedResult<NotificationFeedItemResponse>>(`/api/notifications?page=${page}&pageSize=${pageSize}`),

  getUnreadCount: () => apiFetch<NotificationCountResponse>("/api/notifications/count"),

  markAllRead: () => apiFetch<void>("/api/notifications/read", { method: "POST" }),

  /** Clears one row off the bell, of either kind. The mark, suggestion or status change stays. */
  dismiss: (id: string) => apiFetch<void>(`/api/notifications/${id}/dismiss`, { method: "POST" }),

  dismissAll: () => apiFetch<void>("/api/notifications/dismiss-all", { method: "POST" }),

  /** Undoes an unattended auto-apply. 409 when the application's status has moved on since. */
  revertAutoApply: (suggestionId: string) =>
    apiFetch<void>(`/api/email-forwarding/suggestions/${suggestionId}/revert`, { method: "POST" }),

  getPreferences: () => apiFetch<NotificationPreferences>("/api/users/me/notification-preferences"),

  updatePreferences: (preferences: NotificationPreferences) =>
    apiFetch<NotificationPreferences>("/api/users/me/notification-preferences", {
      method: "PUT",
      body: JSON.stringify(preferences),
    }),
};
