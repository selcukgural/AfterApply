import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { remindersApi, type BreakLength } from "@/lib/api/reminders";
import { remindersQueryKey } from "@/hooks/useReminders";
import { staleApplicationsQueryKey } from "@/hooks/useStaleApplications";
import { ApiError } from "@/lib/api/httpClient";
import type { UndoBulkStatusEntry } from "@/types/api";

/**
 * Under the reminders prefix on purpose: anything that invalidates the reminders list — a status
 * change, a bulk answer — should re-read where the break stands too, since the return question's
 * count is made of reminders.
 */
export const reminderBreakQueryKey = [...remindersQueryKey, "pause"] as const;

export function useReminderBreak() {
  return useQuery({
    queryKey: reminderBreakQueryKey,
    queryFn: remindersApi.getPause,
    staleTime: 60_000,
    retry: (failureCount, error) => !(error instanceof ApiError && error.status === 404) && failureCount < 1,
  });
}

/** Every reader of the break: the dashboard gate, the profile card, and the two things it hides. */
function useInvalidateBreak() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: reminderBreakQueryKey }),
      queryClient.invalidateQueries({ queryKey: staleApplicationsQueryKey }),
    ]);
}

export function useStartBreak() {
  const invalidate = useInvalidateBreak();
  return useMutation({ mutationFn: (days: BreakLength) => remindersApi.pause(days), onSuccess: invalidate });
}

export function useEndBreak() {
  const invalidate = useInvalidateBreak();
  return useMutation({ mutationFn: () => remindersApi.endPause(), onSuccess: invalidate });
}

/** "Not now" to the return question. */
export function useAcknowledgeBreak() {
  const invalidate = useInvalidateBreak();
  return useMutation({ mutationFn: () => remindersApi.acknowledgePause(), onSuccess: invalidate });
}

/** "Yes, close them": a status change on every application that went quiet, so it invalidates
 *  everything a status change feeds — the same set as the card's bulk ghost. */
export function useCloseSilenced() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => remindersApi.closeSilenced(),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: remindersQueryKey }),
        queryClient.invalidateQueries({ queryKey: ["applications"] }),
        queryClient.invalidateQueries({ queryKey: ["analytics"] }),
      ]),
  });
}

export function useUndoCloseSilenced() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (entries: UndoBulkStatusEntry[]) => remindersApi.bulkGhostUndo(entries),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: remindersQueryKey }),
        queryClient.invalidateQueries({ queryKey: ["applications"] }),
        queryClient.invalidateQueries({ queryKey: ["analytics"] }),
      ]),
  });
}
