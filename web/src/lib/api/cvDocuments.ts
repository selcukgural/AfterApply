import type { CvDocumentListResponse, CvDocumentResponse } from "@/types/api";
import { apiFetch, apiFetchBlob } from "./httpClient";

export const cvDocumentsApi = {
  list: () => apiFetch<CvDocumentListResponse>("/api/cv-documents"),

  upload: (file: File) => {
    const body = new FormData();
    body.append("file", file);

    // No Content-Type header: the browser has to set multipart/form-data with its own boundary
    // (see performFetch, which skips the JSON default for FormData bodies).
    return apiFetch<CvDocumentResponse>("/api/cv-documents", { method: "POST", body });
  },

  remove: (id: string) => apiFetch<void>(`/api/cv-documents/${id}`, { method: "DELETE" }),

  setDefault: (id: string) =>
    apiFetch<CvDocumentResponse>(`/api/cv-documents/${id}/default`, { method: "POST" }),

  /** The raw bytes. Used both for the preview (rendered in the browser) and for the save-to-disk
   *  download — one endpoint, because the server never serves a CV any way but as an attachment. */
  content: (id: string) => apiFetchBlob(`/api/cv-documents/${id}/content`),
};

/**
 * Saves a blob to the user's disk under the given name.
 *
 * The download goes through fetch rather than a plain link because the endpoint needs an
 * Authorization header, which a navigation cannot carry — and a link would also mean putting a
 * credential in a URL, which the security baseline rules out.
 */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}
