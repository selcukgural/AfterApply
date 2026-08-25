import type { EmailConnectionStatusResponse, EmailSuggestionResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const emailIntegrationsApi = {
  getStatus: () => apiFetch<EmailConnectionStatusResponse>("/api/email-integrations/gmail/status"),

  getAuthorizationUrl: () =>
    apiFetch<{ authorizationUrl: string }>("/api/email-integrations/gmail/connect"),

  disconnect: () =>
    apiFetch<void>("/api/email-integrations/gmail/disconnect", { method: "POST" }),

  getPendingSuggestions: () =>
    apiFetch<EmailSuggestionResponse[]>("/api/email-integrations/suggestions"),

  confirmSuggestion: (id: string) =>
    apiFetch<void>(`/api/email-integrations/suggestions/${id}/confirm`, { method: "POST" }),

  dismissSuggestion: (id: string) =>
    apiFetch<void>(`/api/email-integrations/suggestions/${id}/dismiss`, { method: "POST" }),
};
