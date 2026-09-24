import type { HubTicketResponse, ImportAcceptedResponse, ImportSummaryResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const importsApi = {
  uploadLinkedInZip: (file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return apiFetch<ImportAcceptedResponse>("/api/imports/linkedin", {
      method: "POST",
      body: formData,
    });
  },

  getImportStatus: (id: string) => apiFetch<ImportSummaryResponse>(`/api/imports/${id}`),

  /** What the progress hub connection authenticates with — never the session token, which the
   * WebSocket handshake would have to carry in its URL. */
  getProgressTicket: () =>
    apiFetch<HubTicketResponse>("/api/imports/progress-ticket", { method: "POST" }).then((r) => r.ticket),
};
