import { useQuery } from "@tanstack/react-query";
import { notificationsApi } from "@/lib/api/notifications";
import { ApiError } from "@/lib/api/httpClient";

export const notificationCountQueryKey = ["notifications", "count"] as const;

// The bell's badge: contribution rows plus the Gmail rows the user has not switched off. A 404 is
// kept as "not a real error" (no retry), the rule every badge poll here follows.
export function useNotificationCount() {
  return useQuery({
    queryKey: notificationCountQueryKey,
    queryFn: () => notificationsApi.getUnreadCount().then((r) => r.unreadCount),
    refetchInterval: 60_000,
    retry: (failureCount, error) => !(error instanceof ApiError && error.status === 404) && failureCount < 1,
  });
}
