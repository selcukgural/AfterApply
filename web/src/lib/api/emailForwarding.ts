import type { EmailSuggestionResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const emailForwardingApi = {
  getAddress: () => apiFetch<{ address: string }>("/api/email-forwarding/address"),

  getPendingSuggestions: () =>
    apiFetch<EmailSuggestionResponse[]>("/api/email-forwarding/suggestions"),

  confirmSuggestion: (id: string) =>
    apiFetch<void>(`/api/email-forwarding/suggestions/${id}/confirm`, { method: "POST" }),

  dismissSuggestion: (id: string) =>
    apiFetch<void>(`/api/email-forwarding/suggestions/${id}/dismiss`, { method: "POST" }),
};
