import type { EmailNotificationResponse, NotificationCountResponse, PagedResult } from "@/types/api";
import { apiFetch } from "./httpClient";

/** How many rows the Notifications page shows at once, and so the page size it asks for. */
export const NOTIFICATION_PAGE_SIZE = 10;

export const notificationsApi = {
  getNotifications: (page: number, pageSize = NOTIFICATION_PAGE_SIZE) =>
    apiFetch<PagedResult<EmailNotificationResponse>>(
      `/api/email-forwarding/notifications?page=${page}&pageSize=${pageSize}`,
    ),

  getUnreadCount: () =>
    apiFetch<NotificationCountResponse>("/api/email-forwarding/notifications/count"),

  markAllRead: () =>
    apiFetch<void>("/api/email-forwarding/notifications/read", { method: "POST" }),

  /** Clears one row off the page. The suggestion and the status change it applied stay. */
  dismiss: (suggestionId: string) =>
    apiFetch<void>(`/api/email-forwarding/notifications/${suggestionId}/dismiss`, { method: "POST" }),

  dismissAll: () =>
    apiFetch<void>("/api/email-forwarding/notifications/dismiss-all", { method: "POST" }),

  /** Undoes an unattended auto-apply. 409 when the application's status has moved on since. */
  revertAutoApply: (suggestionId: string) =>
    apiFetch<void>(`/api/email-forwarding/suggestions/${suggestionId}/revert`, { method: "POST" }),
};
