import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { applicationsApi } from "@/lib/api/applications";
import { ApiError } from "@/lib/api/httpClient";
import { remindersQueryKey } from "@/hooks/useReminders";
import { toUndoEntries } from "@/lib/applications/bulkSelection";
import type { BulkStatusChange } from "@/types/api";

export const staleApplicationsQueryKey = ["applications", "stale"] as const;

export function useStaleSummary() {
  return useQuery({
    queryKey: staleApplicationsQueryKey,
    queryFn: applicationsApi.getStaleSummary,
    retry: (failureCount, error) => !(error instanceof ApiError && error.status === 404) && failureCount < 1,
  });
}

/** Everything a thousand status changes touch at once: counts, list, funnel, reminders, and the
 *  question itself (its key sits under "applications"). */
function useInvalidateAfterStaleChange() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["applications"] }),
      queryClient.invalidateQueries({ queryKey: ["analytics"] }),
      queryClient.invalidateQueries({ queryKey: remindersQueryKey }),
    ]);
}

export function useGhostStale() {
  const invalidate = useInvalidateAfterStaleChange();
  return useMutation({ mutationFn: applicationsApi.ghostStale, onSuccess: invalidate });
}

export function useUndoStaleGhost() {
  const invalidate = useInvalidateAfterStaleChange();
  return useMutation({
    mutationFn: (changes: BulkStatusChange[]) => applicationsApi.undoStaleGhost(toUndoEntries(changes)),
    onSuccess: invalidate,
  });
}

export function useDismissStaleSuggestion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: applicationsApi.dismissStaleSuggestion,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: staleApplicationsQueryKey }),
  });
}
