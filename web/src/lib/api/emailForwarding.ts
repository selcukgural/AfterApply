import type { EmailSuggestionResponse, SuggestionCountResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const emailForwardingApi = {
  getPendingSuggestions: () =>
    apiFetch<EmailSuggestionResponse[]>("/api/email-forwarding/suggestions"),

  getPendingSuggestionCount: () =>
    apiFetch<SuggestionCountResponse>("/api/email-forwarding/suggestions/count"),

  confirmSuggestion: (id: string) =>
    apiFetch<void>(`/api/email-forwarding/suggestions/${id}/confirm`, { method: "POST" }),

  dismissSuggestion: (id: string) =>
    apiFetch<void>(`/api/email-forwarding/suggestions/${id}/dismiss`, { method: "POST" }),
};
