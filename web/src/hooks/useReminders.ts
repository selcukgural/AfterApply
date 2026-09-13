import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { applicationsApi } from "@/lib/api/applications";
import { remindersApi } from "@/lib/api/reminders";
import { ApiError } from "@/lib/api/httpClient";
import type { BulkReminderRequest, ReminderResponse, UndoBulkStatusEntry } from "@/types/api";

/** Prefix of every page's key, so one invalidation drops the whole list. */
export const remindersQueryKey = ["reminders"] as const;

// Same retry policy as useNotificationCount: a 404 is an environment without the feature, not a
// transient fault, and one retry is plenty for everything else. The previous page stays on screen
// while the next loads so the card does not blink to nothing between pages.
export function useReminders(page: number) {
  return useQuery({
    queryKey: [...remindersQueryKey, page],
    queryFn: () => remindersApi.list(page),
    placeholderData: keepPreviousData,
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
 * Everything a status change feeds — the dashboard counts, the list, the funnel — invalidated along
 * with the reminders. Shared by the single-row "mark as ghosted" and the bulk one and its undo.
 */
function useInvalidateAfterStatusChange() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: remindersQueryKey }),
      queryClient.invalidateQueries({ queryKey: ["applications"] }),
      queryClient.invalidateQueries({ queryKey: ["analytics"] }),
    ]);
}

/**
 * Answers a "possibly ghosted" reminder with "yes, it was": a status change on the application,
 * which closes its reminders on the server.
 */
export function useMarkReminderGhosted() {
  const invalidate = useInvalidateAfterStatusChange();
  return useMutation({
    mutationFn: (reminder: ReminderResponse) =>
      applicationsApi.changeStatus(reminder.applicationId, { newStatus: "Ghosted", note: null, changedAt: null }),
    onSuccess: invalidate,
  });
}

/** The three answers for a selection. Dismiss and follow-up touch reminders (and, for follow-up,
 *  the timeline nobody caches); ghosting is a status change and invalidates like one. */
export function useBulkDismissReminders() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: BulkReminderRequest) => remindersApi.bulkDismiss(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: remindersQueryKey }),
  });
}

export function useBulkFollowUpReminders() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: BulkReminderRequest) => remindersApi.bulkFollowUp(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: remindersQueryKey }),
  });
}

export function useBulkGhostReminders() {
  const invalidate = useInvalidateAfterStatusChange();
  return useMutation({
    mutationFn: (request: BulkReminderRequest) => remindersApi.bulkGhost(request),
    onSuccess: invalidate,
  });
}

export function useUndoBulkGhostReminders() {
  const invalidate = useInvalidateAfterStatusChange();
  return useMutation({
    mutationFn: (entries: UndoBulkStatusEntry[]) => remindersApi.bulkGhostUndo(entries),
    onSuccess: invalidate,
  });
}
