"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import type { ApplicationSummaryResponse } from "@/types/api";
import type { ListFilter, SelectionState } from "@/lib/applications/bulkSelection";
import { selectedItems, selectionCount } from "@/lib/applications/bulkSelection";
import { Link } from "@/i18n/navigation";
import { Button } from "@/components/ui/Button";
import { Checkbox } from "@/components/ui/Checkbox";
import { Input } from "@/components/ui/Input";
import { Modal } from "@/components/ui/Modal";
import { StatusBadge } from "@/components/applications/StatusBadge";

/** Long selections are summarised rather than listed in full: past a dozen rows the list stops
 *  being something anyone reads and starts being something they scroll past. */
const MAX_LISTED_ROWS = 8;

interface BulkDeleteDialogProps {
  selection: SelectionState;
  items: readonly ApplicationSummaryResponse[];
  /** The live filter, spelled out for an all-matching delete so "all" has a visible definition. */
  filter: ListFilter;
  /** The company the list is narrowed to, when it is narrowed to one. Named rather than shown as an
   *  id: the scope summary is the only thing standing between the user and a permanent delete, and
   *  a GUID tells them nothing about what it covers. */
  filterCompanyName?: string;
  isSubmitting: boolean;
  error: string | null;
  onConfirm: () => void;
  onClose: () => void;
}

/**
 * The second confirmation for a bulk delete, which is permanent — this product does not soft-delete
 * personal data (DECISIONS.md 2026-09-07), so there is no trash to fish a row back out of.
 *
 * The gate is scaled to the blast radius rather than applied flat: an explicit selection the user
 * can see and count needs an acknowledgement checkbox, while an all-matching delete — whose rows
 * live on pages nobody opened — also asks for the confirmation word to be typed, the same shape the
 * account-deletion flow in Settings already uses.
 */
export function BulkDeleteDialog({
  selection,
  items,
  filter,
  filterCompanyName,
  isSubmitting,
  error,
  onConfirm,
  onClose,
}: BulkDeleteDialogProps) {
  const t = useTranslations("applications.bulk.deleteDialog");
  const tStatus = useTranslations("status");
  const [acknowledged, setAcknowledged] = useState(false);
  const [confirmationText, setConfirmationText] = useState("");

  const count = selectionCount(selection);
  const isAllMatching = selection.kind === "allMatching";
  const rows = selectedItems(selection, items);
  const listedRows = rows.slice(0, MAX_LISTED_ROWS);

  const typedConfirmationOk = !isAllMatching || confirmationText.trim() === t("confirmWord");
  const canDelete = acknowledged && typedConfirmationOk && !isSubmitting;

  return (
    <Modal
      title={t("title", { count })}
      busy={isSubmitting}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={isSubmitting}>
            {t("cancel")}
          </Button>
          <Button variant="danger" onClick={onConfirm} disabled={!canDelete}>
            {isSubmitting ? t("submitting") : t("confirm", { count })}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <div className="flex gap-3">
          <span
            aria-hidden
            className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-crit-wash text-crit-ink"
          >
            <svg width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 9v5" />
              <path d="M12 17.5h.01" />
              <path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z" />
            </svg>
          </span>
          <div className="flex flex-col gap-1.5">
            <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("title", { count })}</h2>
            <p className="text-sm text-gray-600 dark:text-gray-400">
              {isAllMatching ? t("descriptionAllMatching", { count }) : t("description", { count })}
            </p>
          </div>
        </div>

        <div className="flex flex-col gap-1.5 rounded-lg border border-red-200 bg-crit-wash p-3 dark:border-red-900/60">
          <span className="text-sm font-semibold text-crit-ink">{t("cascadeTitle")}</span>
          <span className="text-sm text-crit-ink">{t("cascadeBody")}</span>
          <span className="text-sm text-crit-ink">{t("cascadeKept")}</span>
        </div>

        {isAllMatching ? (
          <dl className="divide-y divide-gray-100 rounded-lg border border-gray-200 text-sm dark:divide-gray-800 dark:border-gray-800">
            <div className="flex gap-3 px-3 py-2">
              <dt className="w-32 shrink-0 text-gray-500 dark:text-gray-400">{t("scopeSearch")}</dt>
              <dd className="text-gray-900 dark:text-gray-100">{filter.search || t("scopeNone")}</dd>
            </div>
            <div className="flex gap-3 px-3 py-2">
              <dt className="w-32 shrink-0 text-gray-500 dark:text-gray-400">{t("scopeStatus")}</dt>
              <dd className="text-gray-900 dark:text-gray-100">
                {filter.status ? tStatus(filter.status) : t("scopeAllStatuses")}
              </dd>
            </div>
            {filter.companyId && (
              <div className="flex gap-3 px-3 py-2">
                <dt className="w-32 shrink-0 text-gray-500 dark:text-gray-400">{t("scopeCompany")}</dt>
                <dd className="text-gray-900 dark:text-gray-100">{filterCompanyName ?? t("scopeNone")}</dd>
              </div>
            )}
            <div className="flex gap-3 px-3 py-2">
              <dt className="w-32 shrink-0 text-gray-500 dark:text-gray-400">{t("scopeMatches")}</dt>
              <dd className="font-semibold text-crit-ink">{t("scopeMatchCount", { count })}</dd>
            </div>
          </dl>
        ) : (
          <div className="rounded-lg border border-gray-200 dark:border-gray-800">
            <div className="flex items-center justify-between border-b border-gray-200 bg-gray-50 px-3 py-2 dark:border-gray-800 dark:bg-gray-800/50">
              <span className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-gray-400">
                {t("listTitle")}
              </span>
              <span className="text-xs text-gray-500 dark:text-gray-400">{t("listCount", { count })}</span>
            </div>
            <ul className="divide-y divide-gray-100 dark:divide-gray-800">
              {listedRows.map((row) => (
                <li key={row.id} className="flex items-center gap-3 px-3 py-2">
                  <span className="w-40 shrink-0 truncate text-sm font-medium text-gray-900 dark:text-gray-100">
                    {row.companyName}
                  </span>
                  <span className="flex-1 truncate text-sm text-gray-500 dark:text-gray-400">{row.jobTitle}</span>
                  <StatusBadge status={row.status} />
                </li>
              ))}
              {rows.length > listedRows.length && (
                <li className="px-3 py-2 text-sm text-gray-500 dark:text-gray-400">
                  {t("listRemaining", { count: rows.length - listedRows.length })}
                </li>
              )}
            </ul>
          </div>
        )}

        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("exportFirst")}{" "}
          <Link href="/settings" className="font-medium text-accent-ink underline underline-offset-2">
            {t("exportLink")}
          </Link>
        </p>

        <Checkbox
          id="bulk-delete-acknowledge"
          checked={acknowledged}
          onChange={(e) => setAcknowledged(e.target.checked)}
          label={t("acknowledge", { count })}
        />

        {isAllMatching && (
          <label className="flex flex-col gap-1.5 text-sm font-medium text-gray-700 dark:text-gray-300">
            {/* The rich text has to sit in its own element: the label is a flex column, so the
                sentence's text nodes and its <strong> would each become a separate flex item and
                the line would break after every one of them. */}
            <span>
              {t.rich("typeToConfirm", { word: (chunks) => <strong className="font-semibold">{chunks}</strong> })}
            </span>
            <Input
              value={confirmationText}
              onChange={(e) => setConfirmationText(e.target.value)}
              placeholder={t("confirmWord")}
              autoComplete="off"
            />
          </label>
        )}

        {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}
      </div>
    </Modal>
  );
}
