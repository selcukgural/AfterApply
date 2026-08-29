import type { ApplicationDetailResponse, ConvertTrackedJobRequest, CreateTrackedJobRequest, TrackedJobResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const trackedJobsApi = {
  getAll: () => apiFetch<TrackedJobResponse[]>("/api/tracked-jobs"),

  create: (request: CreateTrackedJobRequest) =>
    apiFetch<TrackedJobResponse>("/api/tracked-jobs", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  remove: (id: string) =>
    apiFetch<void>(`/api/tracked-jobs/${id}`, {
      method: "DELETE",
    }),

  convert: (id: string, request: ConvertTrackedJobRequest) =>
    apiFetch<ApplicationDetailResponse>(`/api/tracked-jobs/${id}/convert`, {
      method: "POST",
      body: JSON.stringify(request),
    }),
};
