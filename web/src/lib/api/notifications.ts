import type { EmailNotificationResponse, NotificationCountResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const notificationsApi = {
  getNotifications: () =>
    apiFetch<EmailNotificationResponse[]>("/api/email-forwarding/notifications"),

  getUnreadCount: () =>
    apiFetch<NotificationCountResponse>("/api/email-forwarding/notifications/count"),

  markAllRead: () =>
    apiFetch<void>("/api/email-forwarding/notifications/read", { method: "POST" }),

  /** Undoes an unattended auto-apply. 409 when the application's status has moved on since. */
  revertAutoApply: (suggestionId: string) =>
    apiFetch<void>(`/api/email-forwarding/suggestions/${suggestionId}/revert`, { method: "POST" }),
};
