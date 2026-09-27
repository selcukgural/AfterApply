"use client";

import { useTranslations } from "next-intl";
import { Modal } from "@/components/ui/Modal";
import { Button } from "@/components/ui/Button";
import type { ApplicationStatus } from "@/types/api";

/**
 * Asks which stage a card is going to, when its destination does not say: "In progress" holds four
 * stages and "Closed" four endings, and a drop there has to become exactly one status.
 */
export function StagePicker({
  title,
  statuses,
  current,
  onPick,
  onClose,
}: {
  title: string;
  statuses: readonly ApplicationStatus[];
  current: ApplicationStatus | null;
  onPick: (status: ApplicationStatus) => void;
  onClose: () => void;
}) {
  const t = useTranslations("applications.board.stagePicker");
  const tStatus = useTranslations("status");

  return (
    <Modal
      title={t("title")}
      onClose={onClose}
      footer={
        <Button type="button" variant="secondary" onClick={onClose}>
          {t("cancel")}
        </Button>
      }
    >
      <h2 className="text-base font-semibold">{t("title")}</h2>
      <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{title}</p>
      <div className="mt-4 grid grid-cols-1 gap-2 sm:grid-cols-2">
        {statuses.map((status) => (
          <button
            key={status}
            type="button"
            disabled={status === current}
            onClick={() => onPick(status)}
            className="min-h-11 rounded-lg border border-gray-200 px-3 py-2 text-left text-sm font-medium hover:border-accent hover:bg-accent-wash disabled:cursor-default disabled:opacity-50 dark:border-gray-700"
          >
            {tStatus(status)}
          </button>
        ))}
      </div>
    </Modal>
  );
}
