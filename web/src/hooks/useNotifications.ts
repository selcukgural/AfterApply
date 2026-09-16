import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { notificationsApi } from "@/lib/api/notifications";
import { ApiError } from "@/lib/api/httpClient";
import { notificationCountQueryKey } from "@/hooks/useNotificationCount";

/** Prefix of every page's key, so one invalidation drops the whole list. */
export const notificationsQueryKey = ["email-notifications", "list"] as const;

// Same shape as useReminders: a 404 is an environment without the feature, not a transient fault,
// and the previous page stays on screen while the next loads so the list does not blink empty.
export function useNotifications(page: number) {
  return useQuery({
    queryKey: [...notificationsQueryKey, page],
    queryFn: () => notificationsApi.getNotifications(page),
    placeholderData: keepPreviousData,
    retry: (failureCount, error) => !(error instanceof ApiError && error.status === 404) && failureCount < 1,
  });
}

/** Clearing a row changes the list and, for an unread auto-applied one, the nav badge. */
function useInvalidateNotifications() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: notificationsQueryKey }),
      queryClient.invalidateQueries({ queryKey: notificationCountQueryKey }),
    ]);
}

export function useDismissNotification() {
  const invalidate = useInvalidateNotifications();
  return useMutation({
    mutationFn: (id: string) => notificationsApi.dismiss(id),
    onSettled: invalidate,
  });
}

export function useDismissAllNotifications() {
  const invalidate = useInvalidateNotifications();
  return useMutation({
    mutationFn: () => notificationsApi.dismissAll(),
    onSettled: invalidate,
  });
}
