import { useQuery } from "@tanstack/react-query";
import { notificationsApi } from "@/lib/api/notifications";
import { ApiError } from "@/lib/api/httpClient";

export const notificationCountQueryKey = ["email-notifications", "count"] as const;

// A 404 means EmailForwarding:Enabled is off in this environment, not a real error — retrying
// would just hammer the endpoint forever, so this opts out of the global retry, same as
// useSuggestionCount.
export function useNotificationCount() {
  return useQuery({
    queryKey: notificationCountQueryKey,
    queryFn: () => notificationsApi.getUnreadCount().then((r) => r.unreadCount),
    refetchInterval: 60_000,
    retry: (failureCount, error) => !(error instanceof ApiError && error.status === 404) && failureCount < 1,
  });
}
