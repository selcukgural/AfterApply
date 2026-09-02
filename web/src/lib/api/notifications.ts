import type { EmailNotificationResponse, NotificationCountResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const notificationsApi = {
  getNotifications: () =>
    apiFetch<EmailNotificationResponse[]>("/api/email-forwarding/notifications"),

  getUnreadCount: () =>
    apiFetch<NotificationCountResponse>("/api/email-forwarding/notifications/count"),

  markAllRead: () =>
    apiFetch<void>("/api/email-forwarding/notifications/read", { method: "POST" }),
};
