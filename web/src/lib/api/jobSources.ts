import type {
  ApplicationDetailResponse,
  JobSourceDeliveriesResponse,
  JobSourcePostingDetailResponse,
  JobSourceProfileResponse,
  JobSourceStatusResponse,
  UpsertJobSourceProfileRequest,
} from "@/types/api";
import { ApiError, apiFetch } from "./httpClient";

/** The paid weekly job matching. Every route answers 404 while the server flag is off; the
 *  pages check `config.jobSources.enabled` before calling any of these. */
export const jobSourcesApi = {
  getStatus: () => apiFetch<JobSourceStatusResponse>("/api/job-sources/status"),

  getProfile: async (): Promise<JobSourceProfileResponse | null> => {
    try {
      return await apiFetch<JobSourceProfileResponse>("/api/job-sources/profile");
    } catch (error) {
      // No criteria saved yet is a 404, not a failure.
      if (error instanceof ApiError && error.status === 404) {
        return null;
      }
      throw error;
    }
  },

  upsertProfile: (request: UpsertJobSourceProfileRequest) =>
    apiFetch<JobSourceProfileResponse>("/api/job-sources/profile", {
      method: "PUT",
      body: JSON.stringify(request),
    }),

  deleteProfile: () => apiFetch<void>("/api/job-sources/profile", { method: "DELETE" }),

  listPostings: (week?: number) =>
    apiFetch<JobSourceDeliveriesResponse>(`/api/job-sources/postings${week ? `?week=${week}` : ""}`),

  getPosting: (postingId: string) => apiFetch<JobSourcePostingDetailResponse>(`/api/job-sources/postings/${postingId}`),

  markApplied: (postingId: string) =>
    apiFetch<ApplicationDetailResponse>(`/api/job-sources/postings/${postingId}/apply`, { method: "POST" }),
};
