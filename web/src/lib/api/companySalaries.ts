import type {
  CompanySalaryPage,
  HelpfulToggleResponse,
  CompanySalaryRequest,
  CompanySalaryViewerState,
  MyCompanySalary,
  MySalariesResponse,
} from "@/types/api";
import { apiFetch } from "./httpClient";

/** Everything a signed-in person does with salary entries — reading included, there is no
 *  anonymous side. An entry that is not the caller's answers 404 on every write, never 403. */
export const companySalariesApi = {
  list: (companyId: string, page: number) =>
    apiFetch<CompanySalaryPage>(`/api/companies/${companyId}/salaries?page=${page}`),

  viewerState: (companyId: string) =>
    apiFetch<CompanySalaryViewerState>(`/api/companies/${companyId}/salaries/me`),

  create: (companyId: string, request: CompanySalaryRequest) =>
    apiFetch<MyCompanySalary>(`/api/companies/${companyId}/salaries`, { method: "POST", body: JSON.stringify(request) }),

  update: (entryId: string, request: CompanySalaryRequest) =>
    apiFetch<MyCompanySalary>(`/api/company-salaries/${entryId}`, { method: "PUT", body: JSON.stringify(request) }),

  remove: (entryId: string) => apiFetch<void>(`/api/company-salaries/${entryId}`, { method: "DELETE" }),

  /** On, then off. The author's own entry is refused with a 400. */
  toggleHelpful: (entryId: string) =>
    apiFetch<HelpfulToggleResponse>(`/api/company-salaries/${entryId}/helpful`, { method: "POST" }),

  listMine: () => apiFetch<MySalariesResponse>("/api/company-salaries/mine"),
};
