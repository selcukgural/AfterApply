"use client";

import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { Button, buttonClassName } from "@/components/ui/Button";
import { formatCount } from "@/lib/dashboard/format";
import { findingDetails, fixList, pointsAtStake } from "@/lib/cvScan/findings";
import { CvScanCategoryBars, CvScanScoreCard } from "@/components/cvScan/CvScanResultCards";
import type { CvContentNote, CvScanFinding, CvScanResponse } from "@/types/api";

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

  const findings = fixList(result);
  const lost = pointsAtStake(result);

  return (
    <div className="flex flex-col gap-8">
      <CvScanScoreCard score={result.score}>
        <p className="mt-4 border-t border-gray-200 pt-4 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
          {t("result.atsNote")}
        </p>
        <p className="mt-2 text-xs text-gray-500 dark:text-gray-400">{t("result.deterministic")}</p>
      </CvScanScoreCard>

      <CvScanCategoryBars categories={result.categories} />

      <section className="flex flex-col gap-4">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("result.findingsTitle")}</h2>
          {findings.length > 0 ? (
            // A subtraction, not a promise: these are the points the score is missing, so fixing
            // all of them is what it would take to get them back.
            <p className="text-sm text-gray-500 dark:text-gray-400">
              {t("result.findingsSummary", { count: findings.length, points: lost })}
            </p>
          ) : null}
        </div>

        {findings.length === 0 ? (
          <p className="text-sm text-gray-600 dark:text-gray-400">{t("result.findingsNone")}</p>
        ) : (
          <ul className="flex flex-col gap-4">
            {findings.map((finding) => (
              <FindingCard key={finding.code} finding={finding} />
            ))}
          </ul>
        )}
      </section>

      {/* Layer B, and everything about how it is presented follows from one rule: it is not part of
          the score. It sits after the fix list rather than among it, carries a badge saying so, and
          says nothing at all when the feature is off — the reader should never have to work out
          which half of the page the number came from. */}
      {result.reviewStatus !== "Disabled" ? <ContentNotesSection result={result} /> : null}

      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("result.previewTitle")}</h2>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("result.previewHint")}</p>
        <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-gray-500 dark:text-gray-400">
          <span>
            {t("result.documentTitle")}: {result.document.format.toUpperCase()}
          </span>
          <span>
            {result.document.pageCount === null
              ? t("result.documentPagesUnknown")
              : t("result.documentPages", { count: result.document.pageCount })}
          </span>
          <span>{t("result.documentWords", { count: formatCount(result.document.wordCount, locale) })}</span>
        </div>
        {/* Rendered as text in a <pre>, never as markup: this string came out of a file a stranger
            uploaded, and React escaping it is the reason it is safe to show at all. */}
        <pre className="max-h-96 overflow-auto break-words whitespace-pre-wrap rounded-xl border border-gray-200 bg-gray-50 p-4 font-mono text-xs text-gray-800 dark:border-gray-800 dark:bg-gray-950 dark:text-gray-200">
          {result.extractedTextPreview}
        </pre>
        {result.extractedTextTruncated ? (
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("result.previewTruncated")}</p>
        ) : null}
      </section>

      {/* The chain: each step asks for more than the one before it, and the account is last. */}
      <section className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("result.ctaTitle")}</h2>

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

        <p className="text-sm text-gray-600 dark:text-gray-400">{t("result.ctaTrack")}</p>
        <div>
          <Link href="/register" className={buttonClassName()}>
            {t("result.ctaRegister")}
          </Link>
        </div>
      </section>
    </div>
  );
}

function FindingCard({ finding }: { finding: CvScanFinding }) {
  const t = useTranslations("cvScan");
  const details = findingDetails(finding);

  return (
    <li className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100">
          {t(`findings.${finding.code}.title`)}
        </h3>
        <span className="shrink-0 rounded-full bg-gray-100 px-2 py-0.5 text-xs tabular-nums text-gray-700 dark:bg-gray-800 dark:text-gray-300">
          {finding.pointCost > 0 ? t("result.cost", { points: finding.pointCost }) : t("result.costNone")}
        </span>
      </div>

      {details.length > 0 ? (
        <ul className="flex list-disc flex-col gap-1 pl-5 text-sm text-gray-700 dark:text-gray-300">
          {details.map((detail) => (
            <li key={detail.key}>{t(`findings.${finding.code}.details.${detail.key}`, detail.args)}</li>
          ))}
        </ul>
      ) : null}

      {finding.evidence.length > 0 ? (
        <div className="flex flex-col gap-1">
          <p className="text-xs font-medium text-gray-500 dark:text-gray-400">{t("result.evidence")}</p>
          <ul className="flex flex-col gap-1">
            {finding.evidence.slice(0, 3).map((evidence, index) => (
              <li key={index} className="text-xs text-gray-600 dark:text-gray-400">
                <span className="font-medium">
                  {evidence.page === null
                    ? t("result.evidenceDocument")
                    : t("result.evidencePage", { page: evidence.page })}
                </span>
                {evidence.quote ? (
                  // The reader's own line, shown as text. Never dangerouslySetInnerHTML: a CV is
                  // untrusted input, and this is the one place its content reaches the DOM.
                  <span className="ml-2 font-mono">“{evidence.quote}”</span>
                ) : null}
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      <div className="flex flex-col gap-2 border-t border-gray-100 pt-3 text-sm dark:border-gray-800">
        <p className="text-gray-600 dark:text-gray-400">
          <span className="font-medium text-gray-800 dark:text-gray-200">{t("result.why")}: </span>
          {t(`findings.${finding.code}.why`)}
        </p>
        <p className="text-gray-600 dark:text-gray-400">
          <span className="font-medium text-gray-800 dark:text-gray-200">{t("result.fix")}: </span>
          {t(`findings.${finding.code}.fix`)}
        </p>
      </div>
    </li>
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
