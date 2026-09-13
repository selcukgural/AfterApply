"use client";

import { useTranslations } from "next-intl";
import { Card } from "@/components/dashboard/Card";

/** The anchor the detail modal's "grey-area examples" link scrolls to. */
export const MODERATION_GUIDE_GREY_AREAS_ID = "moderation-guide-grey-areas";

const APPROVE_ITEMS = ["workplace", "management", "pay", "career", "balance", "opinion"] as const;
const REJECT_ITEMS = ["insults", "threats", "personalData", "targeting", "accusations", "confidential", "spam", "fake"] as const;
/** Each grey-area row and the verdict it teaches; the wording lives in the catalogue. */
const GREY_CASES = [
  { key: "case1", approve: true },
  { key: "case2", approve: false },
  { key: "case3", approve: true },
  { key: "case4", approve: false },
  { key: "case5", approve: true },
  { key: "case6", approve: false },
  { key: "case7", approve: false },
] as const;

/**
 * The moderation guide, above the queue. It is the one place the rules a review is judged by are
 * written down for the person judging, so every reject lands on the same line — and the two
 * principles at the top are there because the natural drift of a moderator is towards protecting
 * the company, which is not the job. Open/closed is the page's concern (it is remembered).
 */
export function ModerationGuide({ open, onToggle }: { open: boolean; onToggle: (open: boolean) => void }) {
  const t = useTranslations("adminReviews.guide");
  const bodyId = "moderation-guide-body";

  return (
    <Card className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900 dark:text-gray-100">
          <svg aria-hidden="true" width="18" height="18" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" className="text-accent-ink">
            <path d="M4 3h9l3 3v11H4z" />
            <path d="M7 9h6M7 12h6M7 15h4" />
          </svg>
          {t("title")}
        </h2>
        <button
          type="button"
          aria-expanded={open}
          aria-controls={bodyId}
          onClick={() => onToggle(!open)}
          className="text-sm font-medium text-accent-ink hover:underline"
        >
          {open ? t("hide") : t("show")}
        </button>
      </div>

      {open && (
        <div id={bodyId} className="flex flex-col gap-5 text-sm">
          <div className="grid gap-3 sm:grid-cols-2">
            <p className="rounded-lg bg-accent-wash px-4 py-3 font-medium text-gray-900 dark:text-gray-100">{t("principle1")}</p>
            <p className="rounded-lg bg-accent-wash px-4 py-3 font-medium text-gray-900 dark:text-gray-100">{t("principle2")}</p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <h3 className="text-xs font-semibold uppercase tracking-wide text-good-ink">{t("approve.title")}</h3>
              <ul className="flex list-disc flex-col gap-1 pl-5 text-gray-700 dark:text-gray-300">
                {APPROVE_ITEMS.map((item) => (
                  <li key={item}>{t(`approve.${item}`)}</li>
                ))}
              </ul>
            </div>
            <div className="flex flex-col gap-2">
              <h3 className="text-xs font-semibold uppercase tracking-wide text-crit-ink">{t("reject.title")}</h3>
              <ul className="flex list-disc flex-col gap-1 pl-5 text-gray-700 dark:text-gray-300">
                {REJECT_ITEMS.map((item) => (
                  <li key={item}>{t(`reject.${item}`)}</li>
                ))}
              </ul>
            </div>
          </div>

          <div id={MODERATION_GUIDE_GREY_AREAS_ID} className="flex scroll-mt-24 flex-col gap-2">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{t("grey.title")}</h3>
            <p className="text-gray-700 dark:text-gray-300">{t("grey.question")}</p>
            <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-800">
              <table className="w-full min-w-[36rem] border-collapse text-left text-sm">
                <thead>
                  <tr className="bg-gray-50 text-xs uppercase tracking-wide text-gray-500 dark:bg-gray-800/60 dark:text-gray-400">
                    <th className="px-4 py-2 font-semibold">{t("grey.columns.review")}</th>
                    <th className="px-4 py-2 font-semibold">{t("grey.columns.verdict")}</th>
                    <th className="px-4 py-2 font-semibold">{t("grey.columns.why")}</th>
                  </tr>
                </thead>
                <tbody>
                  {GREY_CASES.map(({ key, approve }) => (
                    <tr key={key} className="border-t border-gray-200 align-top text-gray-700 dark:border-gray-800 dark:text-gray-300">
                      <td className="px-4 py-3">{t(`grey.${key}.review`)}</td>
                      <td className="px-4 py-3">
                        <span
                          className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                            approve ? "bg-good-wash text-good-ink" : "bg-crit-wash text-crit-ink"
                          }`}
                        >
                          {approve ? t("grey.approve") : t("grey.reject")}
                        </span>
                      </td>
                      <td className="px-4 py-3">{t(`grey.${key}.why`)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <p className="text-gray-700 dark:text-gray-300">
            <span className="font-semibold text-gray-900 dark:text-gray-100">{t("sendBack.title")}</span> {t("sendBack.body")}
          </p>
        </div>
      )}
    </Card>
  );
}
