import type {
  ApplicationDetailResponse,
  BulkChangeStatusRequest,
  BulkChangeStatusResponse,
  BulkDeleteRequest,
  BulkDeleteResponse,
  UndoBulkStatusEntry,
  UndoBulkStatusResponse,
  ApplicationEventResponse,
  ApplicationEventType,
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
  type: ApplicationEventType;
  /** Null means "now" — the server stamps it. */
  occurredAt: string | null;
  /** Left null by the web app: the server already defaults a manually-added event to Source.Manual,
   *  and letting the client assert its own source would make that claim unverifiable. */
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

  /** Manually-added events only — status changes live in getStatusHistory. The detail view merges
   *  the two (see lib/applications/timeline.ts). Newest first. */
  getTimeline: (id: string) => apiFetch<ApplicationEventResponse[]>(`/api/applications/${id}/timeline`),

  addEvent: (id: string, request: CreateEventRequest) =>
    apiFetch<ApplicationEventResponse>(`/api/applications/${id}/events`, {
      method: "POST",
      body: JSON.stringify(request),
    }),

  /** Throws ApiError with status 409 (body: BulkCountMismatchProblem) when an all-matching
   *  selection no longer matches the count the user was shown. Nothing changed in that case. */
  bulkChangeStatus: (request: BulkChangeStatusRequest) =>
    apiFetch<BulkChangeStatusResponse>("/api/applications/bulk/status", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  /** Built from a bulkChangeStatus response's `changes`. Entries whose status moved on since are
   *  skipped server-side rather than overwritten. */
  undoBulkStatus: (entries: UndoBulkStatusEntry[]) =>
    apiFetch<UndoBulkStatusResponse>("/api/applications/bulk/status/undo", {
      method: "POST",
      body: JSON.stringify({ entries }),
    }),

  /** Permanent. Same 409 contract as bulkChangeStatus. */
  bulkDelete: (request: BulkDeleteRequest) =>
    apiFetch<BulkDeleteResponse>("/api/applications/bulk/delete", {
      method: "POST",
      body: JSON.stringify(request),
    }),
};
