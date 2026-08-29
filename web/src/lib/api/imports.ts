import type { ImportAcceptedResponse, ImportSummaryResponse } from "@/types/api";
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
};
