"use client";

import { useCallback, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { ApplicationStatus, CompanyGroupResponse } from "@/types/api";
import { ApiError } from "@/lib/api/httpClient";
import { applicationsApi } from "@/lib/api/applications";
import {
  EMPTY_SELECTION,
  type ListFilter,
  type SelectionState,
  canOfferAllMatching,
  expectedCountFor,
  groupedPageIds,
  isGroupPartiallySelected,
  isGroupSelected,
  isPagePartiallySelected,
  isRowSelected,
  isSelectionEmpty,
  isWholePageSelected,
  selectAllMatching,
  selectionCount,
  toBulkSelection,
  toUndoEntries,
  toggleAllOnPage,
  toggleGroup,
  toggleRow,
} from "@/lib/applications/bulkSelection";
import {
  parseCompanySortBy,
  parseFlatSortBy,
  parsePage,
  parseSortDirection,
  parseStatus,
  parseView,
} from "@/lib/applications/listView";
import { ApplicationFilters } from "@/components/applications/ApplicationFilters";
import { ApplicationTable } from "@/components/applications/ApplicationTable";
import { ApplicationViewToggle } from "@/components/applications/ApplicationViewToggle";
import { BulkActionBar, BulkSelectAllNotice } from "@/components/applications/BulkActionBar";
import { BulkDeleteDialog } from "@/components/applications/BulkDeleteDialog";
import { type BulkResult, BulkResultBanner } from "@/components/applications/BulkResultBanner";
import { BulkStatusDialog } from "@/components/applications/BulkStatusDialog";
import { CompanyGroupTable } from "@/components/applications/CompanyGroupTable";
import { Pagination } from "@/components/applications/Pagination";
import { Button } from "@/components/ui/Button";

const PAGE_SIZE = 10;
/** Companies, not applications: each one draws its own applications underneath, so ten groups is
 *  already a longer page than ten rows. */
const COMPANY_PAGE_SIZE = 10;

type OpenDialog = "none" | "status" | "delete";

export default function ApplicationsListPage() {
  const t = useTranslations("applications.list");
  const tBulk = useTranslations("applications.bulk");
  const tCommon = useTranslations("common");
  const tErrors = useTranslations("errors");
  const tGroups = useTranslations("applications.groups");
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();

  // Everything that decides what the list shows is read back out of the URL, so a reload, the back
  // button and a pasted link all land on the same screen. Every one of these is validated rather
  // than cast: the two views share `sortBy`, and a value left over from the other view would
  // otherwise be sent to an endpoint that rejects it.
  const view = parseView(searchParams.get("view"));
  const page = parsePage(searchParams.get("page"));
  const search = searchParams.get("search") ?? "";
  const status = parseStatus(searchParams.get("status"));
  const companyId = searchParams.get("companyId") ?? "";
  const sortDirection = parseSortDirection(searchParams.get("sortDirection"));
  const rawSortBy = searchParams.get("sortBy");
  const flatSortBy = parseFlatSortBy(rawSortBy);
  const companySortBy = parseCompanySortBy(rawSortBy);

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

  const flatQuery = useQuery({
    queryKey: ["applications", "list", { page, search, status, companyId, sortBy: flatSortBy, sortDirection }],
    queryFn: () =>
      applicationsApi.getAll({
        page,
        pageSize: PAGE_SIZE,
        search: search || undefined,
        status: status || undefined,
        companyId: companyId || undefined,
        sortBy: flatSortBy,
        sortDirection,
      }),
    enabled: view === "flat",
  });

  const groupedQuery = useQuery({
    queryKey: ["applications", "grouped", { page, search, status, sortBy: companySortBy, sortDirection }],
    queryFn: () =>
      applicationsApi.getGrouped({
        page,
        pageSize: COMPANY_PAGE_SIZE,
        search: search || undefined,
        status: status || undefined,
        sortBy: companySortBy,
        sortDirection,
      }),
    enabled: view === "company",
  });

  const isCompanyView = view === "company";
  const isLoading = isCompanyView ? groupedQuery.isLoading : flatQuery.isLoading;
  const groups = groupedQuery.data?.items ?? [];

  /** The applications on screen, flat, whichever view drew them — what a selection is made of and
   *  what the confirmation dialogs read to name and count rows. */
  const items = isCompanyView ? groups.flatMap((group) => group.applications) : (flatQuery.data?.items ?? []);
  const pageIds = isCompanyView ? groupedPageIds(groups) : items.map((item) => item.id);

  /** The pager counts what the page is made of — companies in one view, applications in the other.
   *  "Select all N matching" always counts applications, because that is what it acts on. */
  const pagerTotalCount = (isCompanyView ? groupedQuery.data?.totalCount : flatQuery.data?.totalCount) ?? 0;
  const matchingApplicationCount =
    (isCompanyView ? groupedQuery.data?.totalApplicationCount : flatQuery.data?.totalCount) ?? 0;
  const hasData = isCompanyView ? groupedQuery.data !== undefined : flatQuery.data !== undefined;

  // The company narrowing is only reachable from the flat list (the company view's "show the
  // remaining N" link), so it never travels with a grouped selection.
  const filter: ListFilter = { search, status, companyId: isCompanyView ? "" : companyId };
  const filterCompanyName = companyId ? items.find((item) => item.companyId === companyId)?.companyName : undefined;

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

  const selectionNotice =
    canOfferAllMatching(selection, pageIds, matchingApplicationCount) || selection.kind === "allMatching" ? (
      <BulkSelectAllNotice
        selection={selection}
        totalCount={matchingApplicationCount}
        onSelectAllMatching={() => setSelection(selectAllMatching(matchingApplicationCount))}
        onClearSelection={() => setSelection(EMPTY_SELECTION)}
      />
    ) : undefined;

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
        view={view}
        leading={
          <ApplicationViewToggle
            view={view}
            onViewChange={(next) =>
              // `sortBy` is dropped rather than carried across: the two views order different
              // things, and the value that made sense in one is not in the other's vocabulary.
              updateParams({ view: next === "flat" ? null : next, sortBy: null, companyId: null })
            }
          />
        }
        search={search}
        status={status}
        sortBy={isCompanyView ? companySortBy : flatSortBy}
        sortDirection={sortDirection}
        onSearchChange={(value) => updateParams({ search: value })}
        onStatusChange={(value) => updateParams({ status: value })}
        onSortByChange={(value) => updateParams({ sortBy: value })}
        onSortDirectionChange={(value) => updateParams({ sortDirection: value })}
      />

      {/* A list narrowed to one company looks like a list that lost most of its rows unless the
          narrowing says so and offers a way out of itself. */}
      {!isCompanyView && companyId && (
        <div className="flex items-center gap-2 self-start rounded-full bg-accent-wash py-1 pl-3 pr-1 text-sm text-accent-ink">
          <span>{tGroups("filteredByCompany", { company: filterCompanyName ?? tGroups("thisCompany") })}</span>
          <button
            type="button"
            onClick={() => updateParams({ companyId: null })}
            aria-label={tGroups("clearCompanyFilter")}
            className="rounded-full p-1 hover:bg-white/60 dark:hover:bg-white/10"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>
      )}

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

      {isLoading || !hasData ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
      ) : (
        <>
          {isCompanyView ? (
            <CompanyGroupTable
              groups={groups}
              selection={{
                isRowSelected: (id) => isRowSelected(selection, id),
                isGroupSelected: (group: CompanyGroupResponse) => isGroupSelected(selection, group),
                isGroupPartiallySelected: (group: CompanyGroupResponse) =>
                  isGroupPartiallySelected(selection, group),
                isWholePageSelected: isWholePageSelected(selection, pageIds),
                isPagePartiallySelected: isPagePartiallySelected(selection, pageIds),
                onToggleRow: (id) => setSelection((current) => toggleRow(current, id, pageIds)),
                onToggleGroup: (group: CompanyGroupResponse) =>
                  setSelection((current) => toggleGroup(current, group, pageIds)),
                onToggleAll: () => setSelection((current) => toggleAllOnPage(current, pageIds)),
                notice: selectionNotice,
              }}
            />
          ) : (
            <ApplicationTable
              items={items}
              selection={{
                isRowSelected: (id) => isRowSelected(selection, id),
                isWholePageSelected: isWholePageSelected(selection, pageIds),
                isPagePartiallySelected: isPagePartiallySelected(selection, pageIds),
                onToggleRow: (id) => setSelection((current) => toggleRow(current, id, pageIds)),
                onToggleAll: () => setSelection((current) => toggleAllOnPage(current, pageIds)),
                notice: selectionNotice,
              }}
            />
          )}
          <Pagination
            page={page}
            pageSize={isCompanyView ? COMPANY_PAGE_SIZE : PAGE_SIZE}
            totalCount={pagerTotalCount}
            unit={isCompanyView ? "companies" : "applications"}
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
          filterCompanyName={filterCompanyName}
          isSubmitting={deleteMutation.isPending}
          error={dialogError}
          onConfirm={() => deleteMutation.mutate()}
          onClose={closeDialog}
        />
      )}
    </div>
  );
}
