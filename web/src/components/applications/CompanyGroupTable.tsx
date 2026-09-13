"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { CompanyGroupResponse } from "@/types/api";
import { areAllExpanded, groupPageKey, initiallyCollapsed, toggleAllCollapsed } from "@/lib/applications/listView";
import { SelectionCheckbox } from "@/components/applications/SelectionCheckbox";
import { StatusBadge } from "@/components/applications/StatusBadge";
import { StatusDistribution } from "@/components/applications/StatusDistribution";
import { buttonClassName } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";

interface GroupSelectionProps {
  isRowSelected: (id: string) => boolean;
  isGroupSelected: (group: CompanyGroupResponse) => boolean;
  isGroupPartiallySelected: (group: CompanyGroupResponse) => boolean;
  isWholePageSelected: boolean;
  isPagePartiallySelected: boolean;
  onToggleRow: (id: string) => void;
  onToggleGroup: (group: CompanyGroupResponse) => void;
  onToggleAll: () => void;
  /** The "select all N matching" strip, composed by the page — see ApplicationTable. */
  notice?: React.ReactNode;
}

/**
 * The applications list with the company as the unit of a row.
 *
 * Groups open expanded rather than collapsed — the reason to be on this screen is to see the
 * applications, and a page of ten collapsed company names shows none of them — except for a group
 * big enough to fill the page on its own; see COLLAPSE_GROUPS_LARGER_THAN. What the user then folds
 * or unfolds is remembered for as long as the page is up and deliberately not put in the URL: which
 * company you folded away is not worth carrying into a shared link. The control in the column header
 * folds or unfolds the whole page at once, for the company with nine applications the user came here
 * to read and would otherwise open one row at a time.
 */
export function CompanyGroupTable({
  groups,
  selection,
}: {
  groups: CompanyGroupResponse[];
  selection?: GroupSelectionProps;
}) {
  const t = useTranslations("applications.table");
  const tGroups = useTranslations("applications.groups");
  const locale = useLocale();
  const [collapsed, setCollapsed] = useState<readonly string[]>(() => initiallyCollapsed(groups));

  // Adjust state during render (the React-documented pattern, as ApplicationFilters already does)
  // rather than in an effect: when the page or the filter moves, the fold state has to go back to
  // the defaults for the companies now on screen, and a carried-over id would fold the wrong row.
  const pageKey = groupPageKey(groups);
  const [previousPageKey, setPreviousPageKey] = useState(pageKey);
  if (pageKey !== previousPageKey) {
    setPreviousPageKey(pageKey);
    setCollapsed(initiallyCollapsed(groups));
  }

  if (groups.length === 0) {
    return (
      <EmptyState
        title={t("empty")}
        body={t("emptyBody")}
        actions={
          <Link href="/applications/new" className={buttonClassName("primary")}>
            {t("emptyCta")}
          </Link>
        }
      />
    );
  }

  const toggleCollapsed = (companyId: string) =>
    setCollapsed((current) =>
      current.includes(companyId) ? current.filter((id) => id !== companyId) : [...current, companyId],
    );

  const allExpanded = areAllExpanded(collapsed);
  const toggleAllLabel = allExpanded ? tGroups("collapseAll") : tGroups("expandAll");

  const columnCount = selection ? 4 : 3;

  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
      <table className="w-full text-left text-sm">
        <thead className="border-b border-gray-200 bg-gray-50 text-xs uppercase text-gray-500 dark:border-gray-800 dark:bg-gray-800 dark:text-gray-400">
          <tr>
            {selection && (
              <th scope="col" className="w-12 py-3 pl-4">
                <SelectionCheckbox
                  checked={selection.isWholePageSelected}
                  indeterminate={selection.isPagePartiallySelected}
                  onChange={selection.onToggleAll}
                  label={t("selectPage")}
                />
              </th>
            )}
            <th scope="col" className="px-4 py-3">
              <span className="flex items-center gap-2">
                {/* The fold-everything control sits in the header of the column whose rows fold,
                    beside the header checkbox that already means "everything on this page" — so it
                    costs the table no row and no width, and reads at the same scope. */}
                <button
                  type="button"
                  onClick={() => setCollapsed((current) => toggleAllCollapsed(groups, current))}
                  aria-expanded={allExpanded}
                  aria-label={toggleAllLabel}
                  title={toggleAllLabel}
                  className="flex h-6 w-6 shrink-0 items-center justify-center rounded border border-gray-300 text-gray-500 transition-colors hover:border-gray-400 hover:text-gray-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-gray-700 dark:text-gray-400 dark:hover:border-gray-500 dark:hover:text-gray-200"
                >
                  <svg
                    aria-hidden
                    width="14"
                    height="14"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    {allExpanded ? (
                      <>
                        <polyline points="7 11 12 6 17 11" />
                        <polyline points="7 17 12 12 17 17" />
                      </>
                    ) : (
                      <>
                        <polyline points="7 7 12 12 17 7" />
                        <polyline points="7 13 12 18 17 13" />
                      </>
                    )}
                  </svg>
                </button>
                <span>{t("company")}</span>
              </span>
            </th>
            <th scope="col" className="px-4 py-3">{tGroups("distribution")}</th>
            <th scope="col" className="px-4 py-3">{tGroups("lastActivity")}</th>
          </tr>
        </thead>

        {selection?.notice && (
          <tbody>
            <tr>
              <td colSpan={columnCount} className="p-0">
                {selection.notice}
              </td>
            </tr>
          </tbody>
        )}

        {groups.map((group) => {
          const isCollapsed = collapsed.includes(group.companyId);
          return (
            <tbody key={group.companyId} className="border-b border-gray-200 last:border-0 dark:border-gray-800">
              <tr className="bg-gray-50/70 dark:bg-gray-800/50">
                {selection && (
                  <td className="py-3 pl-4">
                    <SelectionCheckbox
                      checked={selection.isGroupSelected(group)}
                      indeterminate={selection.isGroupPartiallySelected(group)}
                      onChange={() => selection.onToggleGroup(group)}
                      label={tGroups("selectGroup", { company: group.companyName })}
                    />
                  </td>
                )}
                <td className="px-4 py-3">
                  <button
                    type="button"
                    onClick={() => toggleCollapsed(group.companyId)}
                    aria-expanded={!isCollapsed}
                    className="flex items-center gap-2 rounded text-left font-semibold text-gray-900 focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:text-gray-100"
                  >
                    <svg
                      aria-hidden
                      width="12"
                      height="12"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="3"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      className={`shrink-0 text-gray-400 transition-transform ${isCollapsed ? "" : "rotate-90"}`}
                    >
                      <polyline points="9 6 15 12 9 18" />
                    </svg>
                    <span>{group.companyName}</span>
                    <span className="rounded-full bg-accent-wash px-2 py-0.5 text-xs font-medium text-accent-ink">
                      {tGroups("applicationCount", { count: group.applicationCount })}
                    </span>
                  </button>
                </td>
                <td className="px-4 py-3">
                  <StatusDistribution counts={group.statusCounts} />
                </td>
                <td className="px-4 py-3 text-gray-500 tabular-nums dark:text-gray-400">
                  {new Date(group.lastActivityAt).toLocaleDateString(locale)}
                </td>
              </tr>

              {!isCollapsed &&
                group.applications.map((application) => {
                  const isSelected = selection?.isRowSelected(application.id) ?? false;
                  return (
                    <tr
                      key={application.id}
                      className={
                        isSelected
                          ? "bg-accent-wash hover:bg-accent-wash dark:bg-blue-950/40"
                          : "hover:bg-gray-50 dark:hover:bg-gray-800"
                      }
                    >
                      {selection && (
                        <td className="py-2.5 pl-4">
                          <SelectionCheckbox
                            checked={isSelected}
                            indeterminate={false}
                            onChange={() => selection.onToggleRow(application.id)}
                            label={t("selectRow", {
                              company: application.companyName,
                              position: application.jobTitle,
                            })}
                          />
                        </td>
                      )}
                      <td className="py-2.5 pl-10 pr-4">
                        <Link
                          href={`/applications/${application.id}`}
                          className="text-blue-600 hover:underline dark:text-blue-400"
                        >
                          {application.jobTitle}
                        </Link>
                      </td>
                      <td className="px-4 py-2.5">
                        <StatusBadge status={application.status} />
                      </td>
                      <td className="px-4 py-2.5 text-gray-500 tabular-nums dark:text-gray-400">
                        {new Date(application.appliedAt).toLocaleDateString(locale)}
                      </td>
                    </tr>
                  );
                })}

              {!isCollapsed && group.hasMore && (
                <tr>
                  <td colSpan={columnCount} className="py-2.5 pl-10 pr-4">
                    {/* Out to the flat list filtered to this company: the rest of the rows are paged
                        there, which is the only place they can be shown without this page's size
                        depending on one company's history. */}
                    <Link
                      href={`/applications?companyId=${group.companyId}`}
                      className="text-xs font-medium text-accent-ink hover:underline"
                    >
                      {tGroups("showRemaining", {
                        count: group.applicationCount - group.applications.length,
                        company: group.companyName,
                      })}
                    </Link>
                  </td>
                </tr>
              )}
            </tbody>
          );
        })}
      </table>
    </div>
  );
}
