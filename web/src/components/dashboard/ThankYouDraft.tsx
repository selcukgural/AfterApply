"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { thankYouDraft, type DraftLanguage } from "@/lib/applications/thankYouDraft";
import { useCopy } from "@/hooks/useCopy";

/**
 * A thank-you note to copy, folded under the "how did it go?" row (canvas "İnce dokunuşlar —
 * Paket 6", 6A) — the day after the interview, when one is worth sending. Collapsed by default so
 * the row stays a question; nothing is ever sent from here.
 */
export function ThankYouDraft({ interviewWith, companyName, jobTitle }: {
  interviewWith: string | null | undefined;
  companyName: string;
  jobTitle: string;
}) {
  const t = useTranslations("dashboard.reminders.thankYou");
  const locale = useLocale();
  const [open, setOpen] = useState(false);
  const [language, setLanguage] = useState<DraftLanguage>(locale === "en" ? "en" : "tr");
  const { copied, copy } = useCopy();
  const text = thankYouDraft(language, { interviewWith, companyName, jobTitle });

  if (!open) {
    return (
      <button type="button" onClick={() => setOpen(true)} className="text-xs font-medium text-accent-ink hover:underline">
        {interviewWith ? t("openNamed", { names: interviewWith }) : t("open")}
      </button>
    );
  }

  return (
    <div className="flex flex-col gap-2 rounded-md border border-gray-200 bg-gray-50 p-3 dark:border-gray-800 dark:bg-gray-800/60">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-xs font-semibold text-gray-700 dark:text-gray-300">{t("title")}</span>
        <div className="inline-flex overflow-hidden rounded-md border border-gray-300 text-xs dark:border-gray-700" role="group" aria-label={t("language")}>
          {(["tr", "en"] as const).map((option) => (
            <button
              key={option}
              type="button"
              aria-pressed={language === option}
              onClick={() => setLanguage(option)}
              className={`px-2.5 py-1 ${language === option ? "bg-accent-wash font-medium text-accent-ink" : "bg-white text-gray-700 dark:bg-gray-900 dark:text-gray-300"} ${option === "en" ? "border-l border-gray-300 dark:border-gray-700" : ""}`}
            >
              {option.toUpperCase()}
            </button>
          ))}
        </div>
      </div>
      <p className="whitespace-pre-line text-sm leading-6 text-gray-800 dark:text-gray-200">{text}</p>
      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={() => void copy(text)}
          className="rounded-md bg-accent px-3 py-1.5 text-xs font-medium text-white hover:bg-accent-strong"
        >
          {copied ? t("copied") : t("copy")}
        </button>
        <span className="text-xs text-gray-500 dark:text-gray-400">{t("notSent")}</span>
        <button type="button" onClick={() => setOpen(false)} className="ml-auto text-xs text-gray-500 hover:underline dark:text-gray-400">
          {t("close")}
        </button>
      </div>
    </div>
  );
}
