import type { EmailSuggestionResponse, GmailScanStatusResponse, SuggestionCountResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const emailForwardingApi = {
  getPendingSuggestions: () =>
    apiFetch<EmailSuggestionResponse[]>("/api/email-forwarding/suggestions"),

  getPendingSuggestionCount: () =>
    apiFetch<SuggestionCountResponse>("/api/email-forwarding/suggestions/count"),

  getGmailScanStatus: () =>
    apiFetch<GmailScanStatusResponse>("/api/email-forwarding/gmail-scan-status"),

  confirmSuggestion: (id: string) =>
    apiFetch<void>(`/api/email-forwarding/suggestions/${id}/confirm`, { method: "POST" }),

  dismissSuggestion: (id: string) =>
    apiFetch<void>(`/api/email-forwarding/suggestions/${id}/dismiss`, { method: "POST" }),
};
