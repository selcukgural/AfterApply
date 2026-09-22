"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import type { ApplicationDetailResponse } from "@/types/api";
import {
  formatPromiseDate,
  isClosedStatus,
  promiseDateBounds,
  promiseLine,
  todayDateOnly,
  type PromiseLineTone,
} from "@/lib/applications/replyPromise";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

const TONE: Record<PromiseLineTone, string> = {
  neutral: "text-xs text-gray-500 dark:text-gray-400",
  good: "rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900/40 dark:text-green-300",
  warn: "rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-900 dark:bg-amber-900/40 dark:text-amber-200",
};

interface ReplyPromiseFieldProps {
  application: ApplicationDetailResponse;
  isSaving: boolean;
  error: string | null;
  onSave: (promisedReplyBy: string | null) => Promise<void>;
}

/**
 * The "Söz verilen dönüş" cell of the details grid (design canvas 2026-09-22, variant A): one
 * "+ add a date" link when there is none, the date with its stage and where it stands when there
 * is, and an inline date field for adding or changing it without touching the status. The status
 * panel asks the same question when a stage opens; this cell is for the promise given later, or
 * moved. A closed application with no promise shows nothing — there is nothing left to wait for.
 */
export function ReplyPromiseField({ application, isSaving, error, onSave }: ReplyPromiseFieldProps) {
  const t = useTranslations("applications.detail.promise");
  const tStatus = useTranslations("status");
  const locale = useLocale();
  const [isEditing, setIsEditing] = useState(false);
  const [value, setValue] = useState("");

  const promisedBy = application.promisedReplyBy ?? null;
  const closed = isClosedStatus(application.status);
  if (!promisedBy && closed) return null;

  const today = todayDateOnly();
  const bounds = promiseDateBounds(today);

  const startEditing = () => {
    setValue(promisedBy ?? "");
    setIsEditing(true);
  };

  const save = async () => {
    await onSave(value || null);
    setIsEditing(false);
  };

  const line = promisedBy && application.promisedReplyOutcome
    ? promiseLine(application.promisedReplyOutcome, promisedBy, today)
    : null;

  return (
    <div className="col-span-2 min-w-0">
      <dt className="text-gray-500 dark:text-gray-400">{t("label")}</dt>
      <dd className="flex flex-wrap items-center gap-2 text-gray-900 dark:text-gray-100">
        {isEditing ? (
          <>
            <div className="w-44">
              <Input
                type="date"
                aria-label={t("label")}
                min={bounds.min}
                max={bounds.max}
                value={value}
                onChange={(e) => setValue(e.target.value)}
              />
            </div>
            <Button className="px-3 py-1 text-xs" onClick={save} disabled={isSaving || value === ""}>
              {isSaving ? t("saving") : t("save")}
            </Button>
            <Button variant="secondary" className="px-3 py-1 text-xs" onClick={() => setIsEditing(false)} disabled={isSaving}>
              {t("cancel")}
            </Button>
          </>
        ) : promisedBy ? (
          <>
            <span>{formatPromiseDate(promisedBy, locale)}</span>
            {application.promisedReplyStatus && (
              <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700 dark:bg-gray-800 dark:text-gray-300">
                {t("stage", { status: tStatus(application.promisedReplyStatus) })}
              </span>
            )}
            {line && <span className={TONE[line.tone]}>{t(line.key, { count: line.count })}</span>}
            {!closed && (
              <button type="button" onClick={startEditing} className="text-xs text-blue-600 hover:underline dark:text-blue-400">
                {t("change")}
              </button>
            )}
            <button
              type="button"
              onClick={() => onSave(null)}
              disabled={isSaving}
              className="text-xs text-gray-500 hover:underline disabled:opacity-50 dark:text-gray-400"
            >
              {t("remove")}
            </button>
          </>
        ) : (
          <button type="button" onClick={startEditing} className="text-blue-600 hover:underline dark:text-blue-400">
            {t("add")}
          </button>
        )}
      </dd>
      {error && <p className="mt-1 text-xs text-red-600 dark:text-red-400">{error}</p>}
    </div>
  );
}
