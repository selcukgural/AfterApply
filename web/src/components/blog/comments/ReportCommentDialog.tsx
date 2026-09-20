"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import type { BlogCommentReportReason } from "@/types/api";
import { Modal } from "@/components/ui/Modal";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Textarea } from "@/components/ui/Textarea";

export const COMMENT_REPORT_REASONS: readonly BlogCommentReportReason[] = ["Spam", "Insult", "Inappropriate", "Advertising", "Other"];
export const COMMENT_REPORT_NOTE_MAX_LENGTH = 500;

interface ReportCommentDialogProps {
  onClose: () => void;
  /** Resolves when the report is in; throws to keep the dialog open with the error. */
  onSubmit: (reason: BlogCommentReportReason, note: string | null) => Promise<void>;
  error: string | null;
}

/** "Bildir": one reason from the fixed list, a note required only for "Other" — the
 *  `ReportReviewDialog` shape with radios instead of a select, since there are only five. */
export function ReportCommentDialog({ onClose, onSubmit, error }: ReportCommentDialogProps) {
  const t = useTranslations("blogComments.reportDialog");
  const tReasons = useTranslations("blogComments.reasons");
  const tCommon = useTranslations("blogComments");
  const [reason, setReason] = useState<BlogCommentReportReason>("Spam");
  const [note, setNote] = useState("");
  const [problem, setProblem] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (reason === "Other" && note.trim().length === 0) {
      setProblem(t("noteProblem"));
      return;
    }
    setProblem(null);
    setBusy(true);
    try {
      await onSubmit(reason, note.trim() || null);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      title={t("title")}
      onClose={onClose}
      busy={busy}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={busy}>
            {tCommon("cancel")}
          </Button>
          <Button onClick={submit} disabled={busy}>
            {busy ? t("sending") : t("submit")}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <div>
          <h2 className="text-lg font-semibold">{t("title")}</h2>
          <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("intro")}</p>
        </div>
        <fieldset className="flex flex-col gap-2">
          <legend className="mb-1 text-sm font-medium text-gray-700 dark:text-gray-300">{t("reason")}</legend>
          {COMMENT_REPORT_REASONS.map((option) => (
            <label
              key={option}
              className={`flex cursor-pointer items-center gap-3 rounded-md border px-3 py-2 text-sm ${
                reason === option
                  ? "border-accent bg-accent-wash text-accent-ink"
                  : "border-gray-200 text-gray-900 dark:border-gray-800 dark:text-gray-100"
              }`}
            >
              <input type="radio" name="comment-report-reason" value={option} checked={reason === option} onChange={() => setReason(option)} />
              {tReasons(option)}
            </label>
          ))}
        </fieldset>
        <FormField label={reason === "Other" ? t("noteRequired") : t("note")} htmlFor="comment-report-note" error={problem ?? undefined}>
          <Textarea
            id="comment-report-note"
            rows={3}
            value={note}
            maxLength={COMMENT_REPORT_NOTE_MAX_LENGTH}
            onChange={(e) => setNote(e.target.value)}
            placeholder={t("notePlaceholder")}
          />
        </FormField>
        {error && (
          <p role="alert" className="text-sm text-red-600 dark:text-red-400">
            {error}
          </p>
        )}
      </div>
    </Modal>
  );
}
