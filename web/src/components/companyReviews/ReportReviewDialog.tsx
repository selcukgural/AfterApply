"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import type { ReviewReportReason } from "@/types/api";
import { REPORT_NOTE_MAX_LENGTH, REPORT_REASONS, validateReportDraft } from "@/lib/companyReviews/reviewDraft";
import { Modal } from "@/components/ui/Modal";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Select } from "@/components/ui/Select";
import { Textarea } from "@/components/ui/Textarea";

interface ReportReviewDialogProps {
  onClose: () => void;
  onSubmit: (reason: ReviewReportReason, note: string | null) => Promise<void>;
  error: string | null;
}

/** "Bu yorumu değerlendir": a reason from a fixed list and an optional note (required for Other). */
export function ReportReviewDialog({ onClose, onSubmit, error }: ReportReviewDialogProps) {
  const t = useTranslations("companyReviews.report");
  const tReasons = useTranslations("reviewReportReason");
  const [reason, setReason] = useState<ReviewReportReason>("Spam");
  const [note, setNote] = useState("");
  const [problem, setProblem] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    const draftProblem = validateReportDraft(reason, note);
    if (draftProblem) {
      setProblem(t(`problems.${draftProblem}`));
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
            {t("cancel")}
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
        <FormField label={t("reason")} htmlFor="report-reason">
          <Select id="report-reason" value={reason} onChange={(e) => setReason(e.target.value as ReviewReportReason)}>
            {REPORT_REASONS.map((option) => (
              <option key={option} value={option}>
                {tReasons(option)}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={reason === "Other" ? t("noteRequired") : t("note")} htmlFor="report-note" error={problem ?? undefined}>
          <Textarea
            id="report-note"
            rows={3}
            value={note}
            maxLength={REPORT_NOTE_MAX_LENGTH}
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
