import { useQuery } from "@tanstack/react-query";
import { emailForwardingApi } from "@/lib/api/emailForwarding";
import { ApiError } from "@/lib/api/httpClient";

export const suggestionCountQueryKey = ["email-suggestions", "count"] as const;

// A 404 means EmailForwarding:Enabled is off in this environment, not a real error — retrying
// would just hammer the endpoint forever, so this is the one query in the app that opts out of
// the global retry: 1 default (see QueryProvider).
export function useSuggestionCount() {
  return useQuery({
    queryKey: suggestionCountQueryKey,
    queryFn: () => emailForwardingApi.getPendingSuggestionCount().then((r) => r.count),
    refetchInterval: 60_000,
    retry: (failureCount, error) => !(error instanceof ApiError && error.status === 404) && failureCount < 1,
  });
}
