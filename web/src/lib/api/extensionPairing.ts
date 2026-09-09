import type { ExtensionPairingReviewResponse, ExtensionPairingReviewStatusResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

/**
 * The confirmation half of the extension pairing flow. The other half — starting a request and
 * polling it — belongs to the extension and never runs in this app.
 *
 * The code is put in the path, so it is encoded on the way out; the server normalizes it (case,
 * separators) before looking anything up.
 */
export const extensionPairingApi = {
  get: (code: string) =>
    apiFetch<ExtensionPairingReviewResponse>(`/api/extension-pairing/requests/${encodeURIComponent(code)}`),

  approve: (code: string) =>
    apiFetch<ExtensionPairingReviewStatusResponse>(
      `/api/extension-pairing/requests/${encodeURIComponent(code)}/approve`,
      { method: "POST" },
    ),

  deny: (code: string) =>
    apiFetch<ExtensionPairingReviewStatusResponse>(
      `/api/extension-pairing/requests/${encodeURIComponent(code)}/deny`,
      { method: "POST" },
    ),
};
