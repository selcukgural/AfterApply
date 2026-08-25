import type { CandidateProfileResponse, JobMatchResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const matchingApi = {
  getProfile: () => apiFetch<CandidateProfileResponse>("/api/matching/profile"),

  updateProfile: (cvText: string) =>
    apiFetch<CandidateProfileResponse>("/api/matching/profile", {
      method: "PUT",
      body: JSON.stringify({ cvText }),
    }),

  getMatch: (applicationId: string) =>
    apiFetch<JobMatchResponse>(`/api/matching/applications/${applicationId}`),

  computeMatch: (applicationId: string, jobDescription: string) =>
    apiFetch<JobMatchResponse>(`/api/matching/applications/${applicationId}`, {
      method: "POST",
      body: JSON.stringify({ jobDescription }),
    }),
};
