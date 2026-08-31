import type { EmailSuggestionResponse, InboundAddressResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const emailForwardingApi = {
  getAddress: () => apiFetch<InboundAddressResponse>("/api/email-forwarding/address"),

  getPendingSuggestions: () =>
    apiFetch<EmailSuggestionResponse[]>("/api/email-forwarding/suggestions"),

  confirmSuggestion: (id: string) =>
    apiFetch<void>(`/api/email-forwarding/suggestions/${id}/confirm`, { method: "POST" }),

  dismissSuggestion: (id: string) =>
    apiFetch<void>(`/api/email-forwarding/suggestions/${id}/dismiss`, { method: "POST" }),

  dismissGmailConfirmation: () =>
    apiFetch<void>("/api/email-forwarding/gmail-confirmation/dismiss", { method: "POST" }),
};
