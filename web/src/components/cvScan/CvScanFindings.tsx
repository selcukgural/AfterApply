"use client";

import { useLocale, useTranslations } from "next-intl";
import { formatCount } from "@/lib/dashboard/format";
import { findingDetails, fixList, isNoTextConsequence, pointsAtStake } from "@/lib/cvScan/findings";
import type { CvScanCategoryScore, CvScanDocumentSummary, CvScanFinding } from "@/types/api";

/**
 * The two halves of a scan report that both the public result screen and a stored CV's report
 * on /cv show the same way: the fix list, and the extracted text. They take the slice of a report
 * they need rather than the whole response, so the stored report (no content notes) and the
 * public one share them without either pretending to be the other.
 */

export interface FindingListInput {
  score: number;
  categories: CvScanCategoryScore[];
  findings: CvScanFinding[];
}

export function CvScanFindingList({ result }: { result: FindingListInput }) {
  const t = useTranslations("cvScan");
  const findings = fixList(result);
  const lost = pointsAtStake(result);

  return (
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
  );
}

export interface TextPreviewInput {
  document: CvScanDocumentSummary;
  extractedTextPreview: string;
  extractedTextTruncated: boolean;
}

export function CvScanTextPreview({ result }: { result: TextPreviewInput }) {
  const t = useTranslations("cvScan");
  const locale = useLocale();

  return (
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

      {isNoTextConsequence(finding) ? null : (
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
      )}
    </li>
  );
}

