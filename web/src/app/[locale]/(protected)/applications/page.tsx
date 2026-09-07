"use client";

import { useCallback, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { ApplicationListSortBy, ApplicationStatus, SortDirection } from "@/types/api";
import { ApiError } from "@/lib/api/httpClient";
import { applicationsApi } from "@/lib/api/applications";
import {
  EMPTY_SELECTION,
  type SelectionState,
  canOfferAllMatching,
  expectedCountFor,
  isPagePartiallySelected,
  isRowSelected,
  isSelectionEmpty,
  isWholePageSelected,
  selectAllMatching,
  selectionCount,
  toBulkSelection,
  toUndoEntries,
  toggleAllOnPage,
  toggleRow,
} from "@/lib/applications/bulkSelection";
import { ApplicationFilters } from "@/components/applications/ApplicationFilters";
import { ApplicationTable } from "@/components/applications/ApplicationTable";
import { BulkActionBar, BulkSelectAllNotice } from "@/components/applications/BulkActionBar";
import { BulkDeleteDialog } from "@/components/applications/BulkDeleteDialog";
import { type BulkResult, BulkResultBanner } from "@/components/applications/BulkResultBanner";
import { BulkStatusDialog } from "@/components/applications/BulkStatusDialog";
import { Pagination } from "@/components/applications/Pagination";
import { Button } from "@/components/ui/Button";

const PAGE_SIZE = 10;

type OpenDialog = "none" | "status" | "delete";

export default function ApplicationsListPage() {
  const t = useTranslations("applications.list");
  const tBulk = useTranslations("applications.bulk");
  const tCommon = useTranslations("common");
  const tErrors = useTranslations("errors");
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();

  const page = Number(searchParams.get("page") ?? "1");
  const search = searchParams.get("search") ?? "";
  const status = (searchParams.get("status") as ApplicationStatus | null) ?? "";
  const sortBy = (searchParams.get("sortBy") as ApplicationListSortBy | null) ?? "AppliedAt";
  const sortDirection = (searchParams.get("sortDirection") as SortDirection | null) ?? "Descending";

  const [selection, setSelection] = useState<SelectionState>(EMPTY_SELECTION);
  const [openDialog, setOpenDialog] = useState<OpenDialog>("none");
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [result, setResult] = useState<BulkResult | null>(null);
  const [undoError, setUndoError] = useState<string | null>(null);

  const updateParams = useCallback(
    (updates: Record<string, string | number | null>) => {
      const params = new URLSearchParams(searchParams.toString());
      for (const [key, value] of Object.entries(updates)) {
        if (value === null || value === "") {
          params.delete(key);
        } else {
          params.set(key, String(value));
        }
      }
      // Any filter/sort change resets pagination back to page 1.
      if (!("page" in updates)) {
        params.delete("page");
      }
      // A selection only ever means rows the user can still see, so moving the list out from under
      // it clears it. Without this, "3 selected" would keep pointing at a page they have left.
      setSelection(EMPTY_SELECTION);
      router.push(`/applications?${params.toString()}`);
    },
    [router, searchParams],
  );

  const { data, isLoading } = useQuery({
    queryKey: ["applications", "list", { page, search, status, sortBy, sortDirection }],
    queryFn: () =>
      applicationsApi.getAll({
        page,
        pageSize: PAGE_SIZE,
        search: search || undefined,
        status: status || undefined,
        sortBy,
        sortDirection,
      }),
  });

  const items = data?.items ?? [];
  const pageIds = items.map((item) => item.id);
  const filter: { search: string; status: ApplicationStatus | "" } = { search, status };

  /** Everything a bulk operation invalidates: the list itself and the status counts the dashboard
   *  and the filters read. */
  const refreshLists = useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["applications"] }),
      queryClient.invalidateQueries({ queryKey: ["dashboard"] }),
    ]);
  }, [queryClient]);

  /** A 409 means the list moved under the user and nothing was changed — say which way it moved,
   *  refresh, and let them look again rather than re-submitting for them. */
  const describeError = useCallback(
    (error: unknown): string => {
      if (error instanceof ApiError && error.status === 409) {
        const body = error.body as { actualCount?: number } | undefined;
        return typeof body?.actualCount === "number"
          ? tBulk("countMismatch", { count: body.actualCount })
          : error.message;
      }
      return error instanceof ApiError ? error.message : tErrors("generic");
    },
    [tBulk, tErrors],
  );

  const closeDialog = () => {
    setOpenDialog("none");
    setDialogError(null);
  };

  const finishOperation = () => {
    setSelection(EMPTY_SELECTION);
    closeDialog();
  };

  const changeStatusMutation = useMutation({
    mutationFn: (variables: { newStatus: ApplicationStatus; note: string | null }) =>
      applicationsApi.bulkChangeStatus({
        selection: toBulkSelection(selection, filter),
        newStatus: variables.newStatus,
        note: variables.note,
        expectedCount: expectedCountFor(selection),
      }),
    onSuccess: async (response) => {
      setResult({
        kind: "statusChanged",
        updated: response.updated,
        skipped: response.skippedAlreadyInStatus,
        changes: response.changes,
      });
      setUndoError(null);
      finishOperation();
      await refreshLists();
    },
    onError: async (error) => {
      setDialogError(describeError(error));
      if (error instanceof ApiError && error.status === 409) {
        await refreshLists();
      }
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () =>
      applicationsApi.bulkDelete({
        selection: toBulkSelection(selection, filter),
        expectedCount: expectedCountFor(selection),
      }),
    onSuccess: async (response) => {
      setResult({ kind: "deleted", deleted: response.deleted });
      finishOperation();
      // A delete can empty the page the user is standing on; sending them back to the first page
      // avoids the "no applications found" screen that a now-out-of-range page number would show.
      if (page > 1) {
        updateParams({ page: null });
      }
      await refreshLists();
    },
    onError: async (error) => {
      setDialogError(describeError(error));
      if (error instanceof ApiError && error.status === 409) {
        await refreshLists();
      }
    },
  });

  const undoMutation = useMutation({
    mutationFn: (changes: BulkResult & { kind: "statusChanged" }) =>
      applicationsApi.undoBulkStatus(toUndoEntries(changes.changes)),
    onSuccess: async (response) => {
      setResult({ kind: "statusUndone", reverted: response.reverted, skipped: response.skipped });
      setUndoError(null);
      await refreshLists();
    },
    onError: (error) => setUndoError(describeError(error)),
  });

  return (
    // The floating bar sits over the page, so the list has to give it room while a selection is
    // live — otherwise the last rows are covered by the very toolbar acting on them.
    <div className={`flex flex-col gap-4 ${isSelectionEmpty(selection) ? "" : "pb-24 lg:pb-20"}`}>
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <Link href="/applications/new">
          <Button>{t("newApplication")}</Button>
        </Link>
      </div>

      <ApplicationFilters
        search={search}
        status={status}
        sortBy={sortBy}
        sortDirection={sortDirection}
        onSearchChange={(value) => updateParams({ search: value })}
        onStatusChange={(value) => updateParams({ status: value })}
        onSortByChange={(value) => updateParams({ sortBy: value })}
        onSortDirectionChange={(value) => updateParams({ sortDirection: value })}
      />

      {result && (
        <BulkResultBanner
          result={result}
          isUndoing={undoMutation.isPending}
          undoError={undoError}
          onUndo={() => {
            if (result.kind === "statusChanged") {
              undoMutation.mutate(result);
            }
          }}
          onDismiss={() => {
            setResult(null);
            setUndoError(null);
          }}
        />
      )}

      {isLoading || !data ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
      ) : (
        <>
          <ApplicationTable
            items={data.items}
            selection={{
              isRowSelected: (id) => isRowSelected(selection, id),
              isWholePageSelected: isWholePageSelected(selection, pageIds),
              isPagePartiallySelected: isPagePartiallySelected(selection, pageIds),
              onToggleRow: (id) => setSelection((current) => toggleRow(current, id, pageIds)),
              onToggleAll: () => setSelection((current) => toggleAllOnPage(current, pageIds)),
              notice:
                canOfferAllMatching(selection, pageIds, data.totalCount) || selection.kind === "allMatching" ? (
                  <BulkSelectAllNotice
                    selection={selection}
                    totalCount={data.totalCount}
                    onSelectAllMatching={() => setSelection(selectAllMatching(data.totalCount))}
                    onClearSelection={() => setSelection(EMPTY_SELECTION)}
                  />
                ) : undefined,
            }}
          />
          <Pagination
            page={data.page}
            pageSize={data.pageSize}
            totalCount={data.totalCount}
            onPageChange={(newPage) => updateParams({ page: newPage })}
          />
        </>
      )}

      <BulkActionBar
        selection={selection}
        onClearSelection={() => setSelection(EMPTY_SELECTION)}
        onChangeStatus={() => {
          setDialogError(null);
          setOpenDialog("status");
        }}
        onDelete={() => {
          setDialogError(null);
          setOpenDialog("delete");
        }}
      />

      {openDialog === "status" && !isSelectionEmpty(selection) && (
        <BulkStatusDialog
          selection={selection}
          items={items}
          isSubmitting={changeStatusMutation.isPending}
          error={dialogError}
          onConfirm={(newStatus, note) => changeStatusMutation.mutate({ newStatus, note })}
          onClose={closeDialog}
        />
      )}

      {openDialog === "delete" && !isSelectionEmpty(selection) && (
        <BulkDeleteDialog
          key={selectionCount(selection)}
          selection={selection}
          items={items}
          filter={filter}
          isSubmitting={deleteMutation.isPending}
          error={dialogError}
          onConfirm={() => deleteMutation.mutate()}
          onClose={closeDialog}
        />
      )}
    </div>
  );
}
