import type { CreatedPersonalAccessTokenResponse, PersonalAccessTokenResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const personalAccessTokensApi = {
  list: () => apiFetch<PersonalAccessTokenResponse[]>("/api/personal-access-tokens"),

  create: (name: string) =>
    apiFetch<CreatedPersonalAccessTokenResponse>("/api/personal-access-tokens", {
      method: "POST",
      body: JSON.stringify({ name }),
    }),

  revoke: (id: string) =>
    apiFetch<void>(`/api/personal-access-tokens/${id}`, { method: "DELETE" }),
};
