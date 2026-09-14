import { useQuery } from "@tanstack/react-query";
import { emailForwardingApi } from "@/lib/api/emailForwarding";
import { ApiError } from "@/lib/api/httpClient";

export const gmailScanStatusQueryKey = ["email-suggestions", "gmail-scan-status"] as const;

// Only ever flips from false to true (the first signal creates the row; nothing deletes it short
// of the account), so one fetch per session is plenty. Same 404-means-flag-off handling as
// useSuggestionCount; the pages that read this only use it to pick an empty-state variant, and
// resolveGmailEmptyState treats an error/undefined as "show the nudge".
export function useGmailScanStatus() {
  return useQuery({
    queryKey: gmailScanStatusQueryKey,
    queryFn: () => emailForwardingApi.getGmailScanStatus(),
    staleTime: Infinity,
    retry: (failureCount, error) => !(error instanceof ApiError && error.status === 404) && failureCount < 1,
  });
}
