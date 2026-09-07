import type { FeedbackResponse, SubmitFeedbackRequest } from "@/types/api";
import { apiFetch } from "./httpClient";

export const feedbackApi = {
  submit: (request: SubmitFeedbackRequest) =>
    apiFetch<FeedbackResponse>("/api/feedback", {
      method: "POST",
      body: JSON.stringify(request),
    }),
};
