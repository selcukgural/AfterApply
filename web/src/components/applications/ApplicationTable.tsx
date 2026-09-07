"use client";

import { useEffect, useRef } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { ApplicationSummaryResponse } from "@/types/api";
import { StatusBadge } from "@/components/applications/StatusBadge";

interface SelectionProps {
  isRowSelected: (id: string) => boolean;
  isWholePageSelected: boolean;
  isPagePartiallySelected: boolean;
  onToggleRow: (id: string) => void;
  onToggleAll: () => void;
  /** The "select all N matching" strip, when there is one to show. Rendered as a full-width row
   *  under the header so it reads as part of the table rather than as something floating near it;
   *  composed by the caller, since what it offers is the page's business, not the table's. */
  notice?: React.ReactNode;
}

export function ApplicationTable({
  items,
  selection,
}: {
  items: ApplicationSummaryResponse[];
  selection?: SelectionProps;
}) {
  const t = useTranslations("applications.table");
  const locale = useLocale();

  if (items.length === 0) {
    return <p className="py-8 text-center text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>;
  }

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
            <th className="px-4 py-3">{t("company")}</th>
            <th className="px-4 py-3">{t("position")}</th>
            <th className="px-4 py-3">{t("status")}</th>
            <th className="px-4 py-3">{t("appliedAt")}</th>
          </tr>
        </thead>
        <tbody>
          {selection?.notice && (
            <tr>
              <td colSpan={selection ? 5 : 4} className="p-0">
                {selection.notice}
              </td>
            </tr>
          )}
          {items.map((item) => {
            const isSelected = selection?.isRowSelected(item.id) ?? false;
            return (
              <tr
                key={item.id}
                className={`border-b border-gray-100 last:border-0 dark:border-gray-800 ${
                  isSelected
                    ? "bg-accent-wash hover:bg-accent-wash dark:bg-blue-950/40"
                    : "hover:bg-gray-50 dark:hover:bg-gray-800"
                }`}
              >
                {selection && (
                  <td className="py-3 pl-4">
                    <SelectionCheckbox
                      checked={isSelected}
                      indeterminate={false}
                      onChange={() => selection.onToggleRow(item.id)}
                      label={t("selectRow", { company: item.companyName, position: item.jobTitle })}
                    />
                  </td>
                )}
                <td className="px-4 py-3">
                  <Link
                    href={`/applications/${item.id}`}
                    className="font-medium text-blue-600 hover:underline dark:text-blue-400"
                  >
                    {item.companyName}
                  </Link>
                </td>
                <td className="px-4 py-3 text-gray-700 dark:text-gray-300">{item.jobTitle}</td>
                <td className="px-4 py-3">
                  <StatusBadge status={item.status} />
                </td>
                <td className="px-4 py-3 text-gray-500 dark:text-gray-400">
                  {new Date(item.appliedAt).toLocaleDateString(locale)}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

/** `indeterminate` is a DOM property with no HTML attribute, so it has to be assigned to the node —
 *  React will not set it from JSX. */
function SelectionCheckbox({
  checked,
  indeterminate,
  onChange,
  label,
}: {
  checked: boolean;
  indeterminate: boolean;
  onChange: () => void;
  label: string;
}) {
  const ref = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (ref.current) {
      ref.current.indeterminate = indeterminate;
    }
  }, [indeterminate]);

  return (
    <input
      ref={ref}
      type="checkbox"
      checked={checked}
      onChange={onChange}
      aria-label={label}
      className="h-4 w-4 rounded border-gray-300 text-accent focus:ring-1 focus:ring-blue-500 dark:border-gray-700 dark:bg-gray-900"
    />
  );
}
