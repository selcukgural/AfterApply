import type {
  CandidateExperiencePage,
  CandidateExperienceRequest,
  CandidateExperienceViewerState,
  MyCandidateExperience,
  MyCandidateExperiencesResponse,
} from "@/types/api";
import { apiFetch } from "./httpClient";

/** Candidate experiences: the public list by company slug (anyone can read it), and what a
 *  signed-in person does with their own entry. An entry that is not the caller's answers 404 on
 *  every write, never 403. */
export const candidateExperiencesApi = {
  list: (slug: string, page: number) =>
    apiFetch<CandidateExperiencePage>(`/api/companies/public/${encodeURIComponent(slug)}/experiences?page=${page}`),

  viewerState: (companyId: string) =>
    apiFetch<CandidateExperienceViewerState>(`/api/companies/${companyId}/experiences/me`),

  create: (companyId: string, request: CandidateExperienceRequest) =>
    apiFetch<MyCandidateExperience>(`/api/companies/${companyId}/experiences`, { method: "POST", body: JSON.stringify(request) }),

  update: (experienceId: string, request: CandidateExperienceRequest) =>
    apiFetch<MyCandidateExperience>(`/api/candidate-experiences/${experienceId}`, { method: "PUT", body: JSON.stringify(request) }),

  remove: (experienceId: string) => apiFetch<void>(`/api/candidate-experiences/${experienceId}`, { method: "DELETE" }),

  listMine: () => apiFetch<MyCandidateExperiencesResponse>("/api/candidate-experiences/mine"),
};
