import type { CvScanResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

/**
 * The anonymous CV scan.
 *
 * `/api/cv-scan` is on httpClient's NO_AUTH_ENDPOINTS list, so no Authorization header is attached
 * even when the visitor happens to be signed in — for the same reason the benchmark answer carries
 * none. The scan stores an anonymous score and nothing else; a token arriving with it would make
 * that score attributable to a person, which is the one thing this surface must not do.
 *
 * It goes through `apiFetch` rather than a bare fetch because the refusals here are worth reading:
 * "this file is password protected", "the old .doc format cannot be read". apiFetch is what surfaces
 * a ValidationProblem's localized `errors`.
 */
export interface CvScanInput {
  file: File;
  consentAccepted: boolean;
  /** The optional second consent. False is the ordinary case and costs the caller nothing but the
   *  notes — the score is identical either way. */
  contentNotesRequested: boolean;
  /** Which language the model should write its notes in. The deterministic findings are localized
   *  in the browser from their codes; prose cannot be. */
  locale: string;
  website: string;
  elapsedMs: number;
}

export const cvScanApi = {
  scan: ({ file, consentAccepted, contentNotesRequested, locale, website, elapsedMs }: CvScanInput) => {
    const body = new FormData();
    body.append("file", file);
    body.append("consentAccepted", String(consentAccepted));
    body.append("contentNotesRequested", String(contentNotesRequested));
    body.append("locale", locale);
    // Both anti-abuse fields travel with the file rather than being inferred server-side: the
    // honeypot is a field a person never sees, and elapsedMs is how long the form was open.
    body.append("website", website);
    body.append("elapsedMs", String(elapsedMs));

    // No Content-Type header: the browser sets multipart/form-data with its own boundary.
    return apiFetch<CvScanResponse>("/api/cv-scan", { method: "POST", body });
  },
};
