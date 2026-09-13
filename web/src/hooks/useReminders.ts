import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { applicationsApi } from "@/lib/api/applications";
import { remindersApi } from "@/lib/api/reminders";
import { ApiError } from "@/lib/api/httpClient";
import type { ReminderResponse } from "@/types/api";

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

/** Answers a follow-up reminder with "I followed up": the application gets the event, the row goes. */
export function useFollowUpReminder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => remindersApi.followUp(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: remindersQueryKey }),
  });
}

/**
 * Answers a "possibly ghosted" reminder with "yes, it was": a status change on the application,
 * which closes its reminders on the server. Everything the status feeds — the dashboard counts,
 * the list, the funnel — is invalidated along with the reminders.
 */
export function useMarkReminderGhosted() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (reminder: ReminderResponse) =>
      applicationsApi.changeStatus(reminder.applicationId, { newStatus: "Ghosted", note: null, changedAt: null }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: remindersQueryKey }),
        queryClient.invalidateQueries({ queryKey: ["applications"] }),
        queryClient.invalidateQueries({ queryKey: ["analytics"] }),
      ]),
  });
}
