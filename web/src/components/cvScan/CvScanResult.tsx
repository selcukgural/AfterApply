"use client";

import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { Button, buttonClassName } from "@/components/ui/Button";
import { ShareRow } from "@/components/share/ShareRow";
import { SITE_URL } from "@/lib/seo/routes";
import { cvScanScorePath } from "@/lib/cvScan/path";
import { formatScoreCard } from "@/lib/cvScan/scoreCard";
import { CvScanCategoryBars, CvScanScoreCard } from "@/components/cvScan/CvScanResultCards";
import { CvScanFindingList, CvScanTextPreview } from "@/components/cvScan/CvScanFindings";
import type { CvContentNote, CvScanResponse } from "@/types/api";

/**
 * The result screen. Its whole job is to be checkable: the headline is the sum of the four
 * subtotals shown under it, the fix list adds up to the points missing from the headline, and every
 * finding points at a page and a line of the reader's own file.
 *
 * The sentence about ATS software not auto-rejecting CVs sits next to the score rather than in the
 * explainer below the form, and cannot be moved out of here: it is the correction that keeps a low
 * number from reading as "you are being rejected". (The score card itself lives in
 * CvScanResultCards so the landing page can show it with demo figures; the sentence is passed in
 * from here, and only from here.)
 */
export function CvScanResult({ result, onReset }: { result: CvScanResponse; onReset: () => void }) {
  const t = useTranslations("cvScan");
  const locale = useLocale();

  return (
    <div className="flex flex-col gap-8">
      <CvScanScoreCard score={result.score}>
        <p className="mt-4 border-t border-gray-200 pt-4 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
          {t("result.atsNote")}
        </p>
        <p className="mt-2 text-xs text-gray-500 dark:text-gray-400">{t("result.deterministic")}</p>
      </CvScanScoreCard>

      <CvScanCategoryBars categories={result.categories} />

      <CvScanFindingList result={result} />

      {/* Layer B, and everything about how it is presented follows from one rule: it is not part of
          the score. It sits after the fix list rather than among it, carries a badge saying so, and
          says nothing at all when the feature is off — the reader should never have to work out
          which half of the page the number came from. */}
      {result.reviewStatus !== "Disabled" ? <ContentNotesSection result={result} /> : null}

      <CvScanTextPreview result={result} />

      {/* The chain: each step asks for more than the one before it, and the account is last. */}
      <section className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("result.ctaTitle")}</h2>

        {/* The score is the one thing this page produces that a person might pass on, and the
            sentence asks the question that brings the next person here. The link is the shared-
            score page, whose address carries the score and the four subtotals and nothing else
            (lib/cvScan/scoreCard.ts): the scan stays anonymous, and nothing about this file is
            put anywhere it could be fetched back. */}
        <ShareRow
          label={t("result.shareLabel")}
          content={{
            text: t("result.shareText", { score: result.score }),
            url: `${SITE_URL}/${locale}${cvScanScorePath(locale, formatScoreCard(result.score, result.categories))}`,
          }}
        />

        <p className="text-sm text-gray-600 dark:text-gray-400">{t("result.ctaRescan")}</p>
        <div>
          <Button type="button" variant="secondary" onClick={onReset}>
            {t("form.again")}
          </Button>
        </div>

        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("result.ctaBenchmark")}{" "}
          <Link href="/benchmark" className="text-blue-600 underline underline-offset-2 dark:text-blue-400">
            {t("result.ctaBenchmarkLink")}
          </Link>
        </p>

        {/* The account's reason, stated as what it keeps (growth audit 03b, 2026-09-18): the
            report this page loses on close is the one /cv keeps next to the stored file. */}
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("result.ctaTrack")}</p>
        <div className="flex flex-wrap items-center gap-3">
          <Link href="/register" className={buttonClassName()}>
            {t("result.ctaRegister")}
          </Link>
          <span className="text-xs text-gray-500 dark:text-gray-400">{t("result.ctaTrackNote")}</span>
        </div>
      </section>
    </div>
  );
}

function ContentNotesSection({ result }: { result: CvScanResponse }) {
  const t = useTranslations("cvScan");

  return (
    <section className="flex flex-col gap-3 rounded-xl border border-dashed border-gray-300 p-5 dark:border-gray-700">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("notes.title")}</h2>
        {/* The badge is not decoration: it is the sentence that keeps a reader from reading these
            as points they lost. */}
        <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600 dark:bg-gray-800 dark:text-gray-300">
          {t("notes.badge")}
        </span>
      </div>

      {result.reviewStatus === "NotRequested" ? (
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("notes.notRequested")}</p>
      ) : result.reviewStatus === "Unavailable" ? (
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("notes.unavailable")}</p>
      ) : result.contentNotes.length === 0 ? (
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("notes.none")}</p>
      ) : (
        <ul className="flex flex-col gap-4">
          {result.contentNotes.map((note, index) => (
            <ContentNoteCard key={`${note.kind}-${index}`} note={note} />
          ))}
        </ul>
      )}
    </section>
  );
}

function ContentNoteCard({ note }: { note: CvContentNote }) {
  const t = useTranslations("cvScan");

  return (
    <li className="flex flex-col gap-1">
      <p className="text-sm font-medium text-gray-900 dark:text-gray-100">{t(`notes.kinds.${note.kind}`)}</p>
      {/* Both strings are rendered as text: the quote came out of an uploaded file and the
          suggestion was written by a model that had just read one. React escaping them is what
          makes showing either of them safe. */}
      <p className="font-mono text-xs text-gray-600 dark:text-gray-400">“{note.quote}”</p>
      <p className="text-sm text-gray-700 dark:text-gray-300">{note.suggestion}</p>
    </li>
  );
}
