"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import type { ApplicationStatus, RejectionNotice } from "@/types/api";
import { APPLICATION_STATUSES } from "@/lib/constants/applicationStatus";
import {
  asksForPromise,
  asksForRejectionNotice,
  promiseDateBounds,
  REJECTION_NOTICE_OPTIONS,
  todayDateOnly,
} from "@/lib/applications/replyPromise";
import { Select } from "@/components/ui/Select";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

/** The optional answers the panel collects beside the new status (design canvas 2026-09-22, "Son
 *  hâl"): a reply date for a status still in play, how a rejection was learned of for Rejected.
 *  Each is null when its question was not asked or left blank. */
export interface StatusChangeExtras {
  promisedReplyBy: string | null;
  rejectionNotice: RejectionNotice | null;
}

interface StatusChangeSelectProps {
  currentStatus: ApplicationStatus;
  onChangeStatus: (newStatus: ApplicationStatus, note: string | null, extras: StatusChangeExtras) => Promise<void>;
  isSubmitting: boolean;
}

export function StatusChangeSelect({ currentStatus, onChangeStatus, isSubmitting }: StatusChangeSelectProps) {
  const t = useTranslations("applications.statusChange");
  const tStatus = useTranslations("status");
  const otherStatuses = APPLICATION_STATUSES.filter((s) => s !== currentStatus);
  const [selected, setSelected] = useState<ApplicationStatus>(otherStatuses[0]);
  const [note, setNote] = useState("");
  const [promisedReplyBy, setPromisedReplyBy] = useState("");
  const [rejectionNotice, setRejectionNotice] = useState<RejectionNotice | null>(null);
  const [isOpen, setIsOpen] = useState(false);

  if (!isOpen) {
    return (
      <Button variant="secondary" onClick={() => setIsOpen(true)}>
        {t("changeStatus")}
      </Button>
    );
  }

  const showPromise = asksForPromise(selected);
  const showRejectionNotice = asksForRejectionNotice(selected);
  const bounds = promiseDateBounds(todayDateOnly());

  const reset = () => {
    setIsOpen(false);
    setNote("");
    setPromisedReplyBy("");
    setRejectionNotice(null);
  };

  const handleConfirm = async () => {
    // Only what the current status choice actually asked for travels: a date typed while
    // "Interview" was selected must not ride along once the choice became "Rejected".
    await onChangeStatus(selected, note.trim() || null, {
      promisedReplyBy: showPromise && promisedReplyBy ? promisedReplyBy : null,
      rejectionNotice: showRejectionNotice ? rejectionNotice : null,
    });
    reset();
  };

  return (
    <div className="flex flex-col gap-2 rounded-md border border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-800 p-3">
      <Select aria-label={t("newStatus")} value={selected} onChange={(e) => setSelected(e.target.value as ApplicationStatus)}>
        {otherStatuses.map((status) => (
          <option key={status} value={status}>
            {tStatus(status)}
          </option>
        ))}
      </Select>
      {showRejectionNotice && (
        <fieldset className="flex flex-col gap-2">
          <legend className="pb-1.5 text-sm text-gray-700 dark:text-gray-300">{t("rejectionNoticeLegend")}</legend>
          {REJECTION_NOTICE_OPTIONS.map((option) => {
            const checked = rejectionNotice === option;
            return (
              <label
                key={option}
                className={
                  checked
                    ? "flex min-h-10 cursor-pointer items-center gap-2 rounded-md border-2 border-blue-600 bg-blue-50 px-3 text-sm text-gray-900 dark:border-blue-400 dark:bg-blue-950 dark:text-gray-100"
                    : "flex min-h-10 cursor-pointer items-center gap-2 rounded-md border border-gray-300 bg-white px-3 text-sm text-gray-900 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
                }
              >
                <input
                  type="radio"
                  name="rejection-notice"
                  value={option}
                  checked={checked}
                  onChange={() => setRejectionNotice(option)}
                />
                {t(`rejectionNotice.${option}`)}
              </label>
            );
          })}
        </fieldset>
      )}
      <Input placeholder={t("notePlaceholder")} value={note} onChange={(e) => setNote(e.target.value)} />
      {showPromise && (
        <div className="flex flex-col gap-1">
          <label htmlFor="status-change-promise" className="text-sm text-gray-700 dark:text-gray-300">
            {t("promiseLabel")}
          </label>
          <div className="w-52">
            <Input
              id="status-change-promise"
              type="date"
              min={bounds.min}
              max={bounds.max}
              value={promisedReplyBy}
              onChange={(e) => setPromisedReplyBy(e.target.value)}
            />
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("promiseHint")}</p>
        </div>
      )}
      <div className="flex gap-2">
        <Button onClick={handleConfirm} disabled={isSubmitting}>
          {isSubmitting ? t("saving") : t("confirm")}
        </Button>
        <Button variant="secondary" onClick={reset} disabled={isSubmitting}>
          {t("cancel")}
        </Button>
      </div>
    </div>
  );
}
