"use client";

import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";

interface DeleteApplicationDialogProps {
  companyName: string;
  jobTitle: string;
  isSubmitting: boolean;
  hasError: boolean;
  onConfirm: () => void;
  onClose: () => void;
}

/**
 * Replaces the browser's confirm() for deleting one application. The delete is permanent (no soft
 * delete of personal data, DECISIONS.md 2026-09-07), so the dialog names exactly which application
 * goes and what goes with it, instead of a generic "are you sure?".
 */
export function DeleteApplicationDialog({ companyName, jobTitle, isSubmitting, hasError, onConfirm, onClose }: DeleteApplicationDialogProps) {
  const t = useTranslations("applications.detail.deleteDialog");

  return (
    <Modal
      title={t("title")}
      busy={isSubmitting}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={isSubmitting} data-autofocus>
            {t("cancel")}
          </Button>
          <Button variant="danger" onClick={onConfirm} disabled={isSubmitting}>
            {isSubmitting ? t("deleting") : t("confirm")}
          </Button>
        </>
      }
    >
      {/* Same shape as the bulk delete's header, so the two destructive dialogs read alike. */}
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
          <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
          <p className="text-sm text-gray-600 dark:text-gray-400">
            {t.rich("body", {
              application: () => (
                <strong className="font-medium text-gray-900 dark:text-gray-100">
                  {companyName} — {jobTitle}
                </strong>
              ),
            })}
          </p>
          {hasError ? <p className="text-sm text-red-600 dark:text-red-400">{t("error")}</p> : null}
        </div>
      </div>
    </Modal>
  );
}
