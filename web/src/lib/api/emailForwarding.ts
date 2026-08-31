import { apiFetch } from "./httpClient";

export const emailForwardingApi = {
  getAddress: () => apiFetch<{ address: string }>("/api/email-forwarding/address"),
};
