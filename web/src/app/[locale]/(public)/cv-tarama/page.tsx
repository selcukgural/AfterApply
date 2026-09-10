import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { CvScanForm } from "@/components/cvScan/CvScanForm";

/**
 * The sign-up-free CV scan — the lower of the two public doorsteps. The benchmark asks a stranger
 * to type in their own numbers; this one lets them arrive with a file they already have.
 *
 * **The order of this page is a decision, not a layout accident: the upload comes first and the
 * explanation second.** Someone who already knows what an ATS is should not have to scroll past a
 * paragraph explaining it, and the page's whole promise is an answer rather than an argument. The
 * cost is that a reader can upload before reading the correction to the "75% auto-reject" myth —
 * paid for by the same correction sitting next to the score on the result screen, where it cannot
 * be scrolled past.
 *
 * The path is one shared segment for both languages rather than a translated one, like /benchmark:
 * localising it means adding `pathnames` to next-intl's routing config, which retypes `Link`'s href
 * across the whole app. The Turkish slug is deliberate — "cv tarama" is the phrase the audience
 * this page is built for actually searches.
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/cv-tarama">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/cv-tarama", "cvScan");
}

export default async function CvScanPage() {
  const t = await getTranslations("cvScan");

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-10 px-4 py-12">
      <header className="flex flex-col gap-3">
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl dark:text-gray-100">
          {t("title")}
        </h1>
        <p className="max-w-[60ch] text-lg text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </header>

      <CvScanForm />

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
    </div>
  );
}
