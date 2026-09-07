"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import type { ApplicationStatus, ApplicationSummaryResponse } from "@/types/api";
import type { SelectionState } from "@/lib/applications/bulkSelection";
import { countAlreadyInStatus, defaultTargetStatus, selectionCount } from "@/lib/applications/bulkSelection";
import { APPLICATION_STATUSES } from "@/lib/constants/applicationStatus";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Modal } from "@/components/ui/Modal";
import { Select } from "@/components/ui/Select";

interface BulkStatusDialogProps {
  selection: SelectionState;
  /** The rows currently on screen — used only to work out how many of an explicit selection are
   *  already in the chosen status. */
  items: readonly ApplicationSummaryResponse[];
  isSubmitting: boolean;
  error: string | null;
  onConfirm: (newStatus: ApplicationStatus, note: string | null) => void;
  onClose: () => void;
}

/**
 * The second confirmation for a bulk status change. It is a dialog rather than a checkbox gate
 * because this operation is reversible — the undo strip on the list puts it straight back — so the
 * deliberate act is choosing the status and pressing a button that names the count, not swearing an
 * oath about permanence. Bulk *delete* is the one that asks for more.
 */
export function BulkStatusDialog({
  selection,
  items,
  isSubmitting,
  error,
  onConfirm,
  onClose,
}: BulkStatusDialogProps) {
  const t = useTranslations("applications.bulk.statusDialog");
  const tStatus = useTranslations("status");
  const [newStatus, setNewStatus] = useState<ApplicationStatus>(() =>
    defaultTargetStatus(selection, items, APPLICATION_STATUSES),
  );
  const [note, setNote] = useState("");

  const total = selectionCount(selection);
  // Null for an all-matching selection: the already-in-status rows are spread across pages this
  // screen has never loaded, and a confirmation that guesses is worse than one that stays quiet.
  const alreadyInStatus = countAlreadyInStatus(selection, items, newStatus);
  const willChange = alreadyInStatus === null ? null : total - alreadyInStatus;

  return (
    <Modal
      title={t("title", { count: total })}
      busy={isSubmitting}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={isSubmitting}>
            {t("cancel")}
          </Button>
          <Button onClick={() => onConfirm(newStatus, note.trim() || null)} disabled={isSubmitting || willChange === 0}>
            {isSubmitting
              ? t("submitting")
              : willChange === null
                ? t("confirmUnknownCount", { count: total })
                : t("confirm", { count: willChange })}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("title", { count: total })}</h2>
          <p className="text-sm text-gray-600 dark:text-gray-400">{t("description")}</p>
        </div>

        <label className="flex flex-col gap-1.5 text-sm font-medium text-gray-700 dark:text-gray-300">
          {t("newStatus")}
          <Select value={newStatus} onChange={(e) => setNewStatus(e.target.value as ApplicationStatus)}>
            {APPLICATION_STATUSES.map((status) => (
              <option key={status} value={status}>
                {tStatus(status)}
              </option>
            ))}
          </Select>
        </label>

        <label className="flex flex-col gap-1.5 text-sm font-medium text-gray-700 dark:text-gray-300">
          {t("noteLabel", { count: total })}
          <Input placeholder={t("notePlaceholder")} value={note} onChange={(e) => setNote(e.target.value)} />
        </label>

        <div className="flex flex-col gap-2 rounded-lg border border-gray-200 bg-gray-50 p-3 dark:border-gray-800 dark:bg-gray-800/50">
          {willChange === null || alreadyInStatus === null ? (
            <p className="text-sm text-gray-700 dark:text-gray-300">{t("summaryUnknown", { count: total })}</p>
          ) : (
            <>
              <p className="text-sm text-gray-700 dark:text-gray-300">
                {t("summaryWillChange", { count: willChange, status: tStatus(newStatus) })}
              </p>
              {alreadyInStatus > 0 && (
                <p className="text-sm text-gray-700 dark:text-gray-300">
                  {t("summaryAlreadyInStatus", { count: alreadyInStatus })}
                </p>
              )}
            </>
          )}
        </div>

        <p className="text-sm text-gray-500 dark:text-gray-400">{t("reversible")}</p>

        {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}
      </div>
    </Modal>
  );
}
