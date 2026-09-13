import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { remindersApi } from "@/lib/api/reminders";
import { ApiError } from "@/lib/api/httpClient";

export const remindersQueryKey = ["reminders"] as const;

// Same retry policy as useNotificationCount: a 404 is an environment without the feature, not a
// transient fault, and one retry is plenty for everything else.
export function useReminders() {
  return useQuery({
    queryKey: remindersQueryKey,
    queryFn: remindersApi.list,
    refetchInterval: 60_000,
    retry: (failureCount, error) => !(error instanceof ApiError && error.status === 404) && failureCount < 1,
  });
}

export function useDismissReminder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => remindersApi.dismiss(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: remindersQueryKey }),
  });
}
