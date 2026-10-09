import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { applicationsApi } from "@/lib/api/applications";
import { remindersApi, type SnoozeDays } from "@/lib/api/reminders";
import { ApiError } from "@/lib/api/httpClient";
import type {
  BulkReminderRequest,
  InterviewOutcomeRequest,
  InterviewOutcomeResponse,
  ReminderResponse,
  UndoBulkStatusEntry,
} from "@/types/api";

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

/** The interviews of the next two weeks, for the top of the reminders card. Same retry policy as
 *  the list: an API without the route is an environment without the feature. */
export function useUpcomingInterviews() {
  return useQuery({
    queryKey: [...remindersQueryKey, "interviews"],
    queryFn: () => remindersApi.upcomingInterviews(),
    refetchInterval: 60_000,
    retry: (failureCount, error) => !(error instanceof ApiError && error.status === 404) && failureCount < 1,
  });
}

export function useSnoozeReminder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, days }: { id: string; days: SnoozeDays }) => remindersApi.snooze(id, days),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: remindersQueryKey }),
  });
}

export function useUnsnoozeReminder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => remindersApi.unsnooze(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: remindersQueryKey }),
  });
}

/** "How did it go?" — a status change or a reply date behind it, so it invalidates like one. */
export function useAnswerInterview() {
  const invalidate = useInvalidateAfterStatusChange();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: InterviewOutcomeRequest }) =>
      remindersApi.answerInterview(id, request),
    onSuccess: invalidate,
  });
}

export function useUndoInterviewAnswer() {
  const invalidate = useInvalidateAfterStatusChange();
  return useMutation({
    mutationFn: ({ id, outcome }: { id: string; outcome: InterviewOutcomeResponse }) =>
      remindersApi.undoInterviewAnswer(id, outcome),
    onSuccess: invalidate,
  });
}

/** "Apply here again" reminders whose day has come — their own card, outside the paged list. */
export function useReapplyReminders() {
  return useQuery({
    queryKey: [...remindersQueryKey, "reapply"],
    queryFn: () => remindersApi.reapply(),
    retry: (failureCount, error) => !(error instanceof ApiError && error.status === 404) && failureCount < 1,
  });
}

/** Sets, moves, declines or cancels the "apply here again" reminder of a rejected application:
 *  `months` null declines. The application page and the dashboard card both read the result. */
export function useSetReapplyReminder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ applicationId, months }: { applicationId: string; months: 3 | 6 | 12 | null }) =>
      months === null
        ? applicationsApi.declineReapplyReminder(applicationId)
        : applicationsApi.setReapplyReminder(applicationId, { months }),
    onSuccess: (_, { applicationId }) => {
      queryClient.invalidateQueries({ queryKey: remindersQueryKey });
      queryClient.invalidateQueries({ queryKey: ["applications", "detail", applicationId] });
    },
  });
}
