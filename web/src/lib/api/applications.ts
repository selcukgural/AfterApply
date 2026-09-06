import type {
  ApplicationDetailResponse,
  ApplicationEventResponse,
  ApplicationListQuery,
  ApplicationSummaryCountsResponse,
  ApplicationStatusHistoryResponse,
  ApplicationSummaryResponse,
  ChangeStatusRequest,
  CreateApplicationRequest,
  PagedResult,
  UpdateApplicationRequest,
} from "@/types/api";
import { apiFetch } from "./httpClient";

export interface CreateEventRequest {
  type: string;
  occurredAt: string | null;
  source: string | null;
  metadata: string | null;
}

function buildQueryString(query: ApplicationListQuery): string {
  const params = new URLSearchParams();
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  if (query.search) params.set("search", query.search);
  if (query.status) params.set("status", query.status);
  if (query.sortBy) params.set("sortBy", query.sortBy);
  if (query.sortDirection) params.set("sortDirection", query.sortDirection);
  const qs = params.toString();
  return qs ? `?${qs}` : "";
}

export const applicationsApi = {
  getAll: (query: ApplicationListQuery) =>
    apiFetch<PagedResult<ApplicationSummaryResponse>>(`/api/applications${buildQueryString(query)}`),

  getSummary: () => apiFetch<ApplicationSummaryCountsResponse>("/api/applications/summary"),

  getById: (id: string) => apiFetch<ApplicationDetailResponse>(`/api/applications/${id}`),

  create: (request: CreateApplicationRequest) =>
    apiFetch<ApplicationDetailResponse>("/api/applications", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  update: (id: string, request: UpdateApplicationRequest) =>
    apiFetch<ApplicationDetailResponse>(`/api/applications/${id}`, {
      method: "PUT",
      body: JSON.stringify(request),
    }),

  remove: (id: string) =>
    apiFetch<void>(`/api/applications/${id}`, {
      method: "DELETE",
    }),

  changeStatus: (id: string, request: ChangeStatusRequest) =>
    apiFetch<ApplicationDetailResponse>(`/api/applications/${id}/status`, {
      method: "POST",
      body: JSON.stringify(request),
    }),

  getStatusHistory: (id: string) =>
    apiFetch<ApplicationStatusHistoryResponse[]>(`/api/applications/${id}/status-history`),

  addEvent: (id: string, request: CreateEventRequest) =>
    apiFetch<ApplicationEventResponse>(`/api/applications/${id}/events`, {
      method: "POST",
      body: JSON.stringify(request),
    }),
};
