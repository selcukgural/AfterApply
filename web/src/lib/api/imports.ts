import type { ImportSummaryResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const importsApi = {
  uploadLinkedInZip: (file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return apiFetch<ImportSummaryResponse>("/api/imports/linkedin", {
      method: "POST",
      body: formData,
    });
  },
};
