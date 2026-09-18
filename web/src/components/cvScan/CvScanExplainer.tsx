import { getTranslations } from "next-intl/server";

/**
 * Everything the scan page says *after* the upload form — what the result will show, what
 * survives the request, what an ATS is and the correction to the "75% auto-reject" number, and
 * what the tool cannot do. One component for the two pages that carry it: the scan page itself and
 * the shared-score page (`/cv-tarama/puan/<card>`), which opens with someone else's number and
 * then has to be the same page in every other respect.
 */
export async function CvScanExplainer() {
  const t = await getTranslations("cvScan");

  return (
    <>
      <section className="flex flex-col gap-3 border-t border-gray-200 pt-8 dark:border-gray-800">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("afterScan.title")}</h2>
        <ul className="flex list-disc flex-col gap-2 pl-5 text-sm text-gray-600 dark:text-gray-400">
          <li>{t("afterScan.score")}</li>
          <li>{t("afterScan.rawText")}</li>
          <li>{t("afterScan.fixes")}</li>
        </ul>
      </section>

      {/* The retention list, written out in full. It is the same list as the privacy policy's,
          word for word, because a page that reads a stranger's CV without an account has to say
          exactly what survives the request — and "nothing" is only believable when it is itemised. */}
      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("retained.title")}</h2>
        <ul className="flex list-disc flex-col gap-2 pl-5 text-sm text-gray-600 dark:text-gray-400">
          <li>{t("retained.ip")}</li>
          <li>{t("retained.score")}</li>
          <li>{t("retained.counter")}</li>
        </ul>
        <p className="text-sm font-medium text-gray-800 dark:text-gray-200">{t("retained.none")}</p>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("ats.title")}</h2>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("ats.what")}</p>
        {/* The correction to the number everyone repeats. Sourced in DEVELOPMENT_PLAN.md (V6):
            the 75% figure traces to a 2012 sales brochure, and a 2025 survey has ~92% of recruiters
            saying they set no automatic rejection rules. */}
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("ats.myth")}</p>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("ats.truth")}</p>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("limits.title")}</h2>
        <ul className="flex list-disc flex-col gap-2 pl-5 text-sm text-gray-600 dark:text-gray-400">
          <li>{t("limits.noJobMatch")}</li>
          <li>{t("limits.noAts")}</li>
          <li>{t("limits.noContent")}</li>
        </ul>
      </section>
    </>
  );
}
