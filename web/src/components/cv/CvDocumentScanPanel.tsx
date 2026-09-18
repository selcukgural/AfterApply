"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Button } from "@/components/ui/Button";
import { ShareRow } from "@/components/share/ShareRow";
import { CvScanCategoryBars, CvScanScoreCard } from "@/components/cvScan/CvScanResultCards";
import { CvScanFindingList, CvScanTextPreview } from "@/components/cvScan/CvScanFindings";
import { useClientConfig } from "@/hooks/useClientConfig";
import { cvDocumentsApi } from "@/lib/api/cvDocuments";
import { ApiError } from "@/lib/api/httpClient";
import { cvScanScorePath } from "@/lib/cvScan/path";
import { formatScoreCard } from "@/lib/cvScan/scoreCard";
import { SITE_URL } from "@/lib/seo/routes";
import type { CvDocumentResponse } from "@/types/api";

interface CvDocumentScanPanelProps {
  document: CvDocumentResponse;
  /** The list is what carries the score; a scan has to refresh it as well as this panel. */
  onScanned: () => Promise<unknown>;
}

/**
 * The ATS-readability section of a stored CV on /cv (growth audit 2026-09-14, finding 03b;
 * decided 2026-09-18, option A): the public scan's report, kept with the file it describes. Not
 * run on upload — uploading a CV keeps meaning what it meant — but on a button, and re-run on
 * another. The report opens in place under the score; the stored one is read back rather than
 * the file re-read, so opening it costs nothing.
 *
 * This is where the anonymous scan's "open an account" sentence points: the report that page
 * loses on close is the one this panel keeps.
 */
export function CvDocumentScanPanel({ document, onScanned }: CvDocumentScanPanelProps) {
  const t = useTranslations("cv.scan");
  const tScan = useTranslations("cvScan");
  const locale = useLocale();
  const queryClient = useQueryClient();
  const { config } = useClientConfig();
  const [reportOpen, setReportOpen] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const reportKey = ["cvDocuments", document.id, "scan"] as const;

  const report = useQuery({
    queryKey: reportKey,
    queryFn: () => cvDocumentsApi.scanReport(document.id),
    // Only asked for when there is one to read and the person opened it.
    enabled: reportOpen && document.scan !== null,
  });

  const scanMutation = useMutation({
    mutationFn: () => cvDocumentsApi.scan(document.id),
    onSuccess: async (written) => {
      setErrorMessage(null);
      queryClient.setQueryData(reportKey, written);
      setReportOpen(true);
      await onScanned();
    },
    onError: (error) => setErrorMessage(error instanceof ApiError ? error.message : t("error")),
  });

  if (!config.cvScan.enabled) return null;

  const scan = document.scan;

  return (
    <div className="flex flex-col gap-3 border-t border-gray-200 pt-4 dark:border-gray-800">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex min-w-0 flex-col gap-0.5">
          <p className="text-sm font-medium text-gray-900 dark:text-gray-100">{t("title")}</p>
          {scan ? (
            <p className="text-sm text-gray-500 dark:text-gray-400">
              <span className="font-medium tabular-nums text-gray-900 dark:text-gray-100">{t("score", { score: scan.score })}</span>{" "}
              {t("measuredAt", { date: new Date(scan.scannedAt).toLocaleDateString(locale) })}
            </p>
          ) : (
            <p className="text-sm text-gray-500 dark:text-gray-400">{t("description")}</p>
          )}
        </div>
        <div className="flex shrink-0 flex-wrap gap-2">
          {scan ? (
            <Button variant="secondary" onClick={() => setReportOpen((open) => !open)}>
              {reportOpen ? t("closeReport") : t("openReport")}
            </Button>
          ) : null}
          <Button
            variant={scan ? "secondary" : "primary"}
            onClick={() => scanMutation.mutate()}
            disabled={scanMutation.isPending}
          >
            {scanMutation.isPending ? t("measuring") : scan ? t("measureAgain") : t("measure")}
          </Button>
        </div>
      </div>

      {errorMessage ? (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {errorMessage}
        </p>
      ) : null}

      {reportOpen && scan ? (
        report.data ? (
          <div className="flex flex-col gap-6 pt-2">
            <CvScanScoreCard score={report.data.score}>
              <p className="mt-4 border-t border-gray-200 pt-4 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                {tScan("result.atsNote")}
              </p>
            </CvScanScoreCard>
            <CvScanCategoryBars categories={report.data.categories} />
            <CvScanFindingList result={report.data} />
            <CvScanTextPreview result={report.data} />
            {/* Same card as the public result: the score and the four subtotals in the address,
                nothing about the file — see lib/cvScan/scoreCard.ts. */}
            <ShareRow
              label={tScan("result.shareLabel")}
              content={{
                text: tScan("result.shareText", { score: report.data.score }),
                url: `${SITE_URL}/${locale}${cvScanScorePath(locale, formatScoreCard(report.data.score, report.data.categories))}`,
              }}
            />
          </div>
        ) : report.isError ? (
          <p role="alert" className="text-sm text-red-600 dark:text-red-400">
            {t("error")}
          </p>
        ) : (
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>
        )
      ) : null}
    </div>
  );
}
