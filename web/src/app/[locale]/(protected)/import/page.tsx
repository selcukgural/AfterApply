"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { LinkedInImportUploader } from "@/components/imports/LinkedInImportUploader";
import { LinkedInArchiveDiagram } from "@/components/imports/LinkedInArchiveDiagram";
import { archiveDiagramLabels } from "@/components/imports/archiveDiagramLabels";
import { buttonClassName } from "@/components/ui/Button";

// LinkedIn's own "Get a copy of your data" screen. Hard-coded rather than assembled from user
// data, so it never needs safeExternalUrl — but it still opens in a new tab with noopener, since
// this page can be mid-upload and must not be navigated away from.
const LINKEDIN_DATA_EXPORT_URL = "https://www.linkedin.com/mypreferences/d/download-my-data";

function Step({ number, title, children }: { number: number; title: string; children: React.ReactNode }) {
  return (
    <section className="flex gap-4 rounded-lg border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900">
      <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-accent text-sm font-semibold text-white">
        {number}
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-3">
        <h2 className="font-semibold text-gray-900 dark:text-gray-100">{title}</h2>
        {children}
      </div>
    </section>
  );
}

export default function ImportPage() {
  const t = useTranslations("imports");
  const tDiagram = useTranslations("imports.diagram");

  const stepper = [t("stepper.request"), t("stepper.download"), t("stepper.upload")];

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6">
      <div className="flex flex-col gap-2">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </div>

      <ol className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-gray-500 dark:text-gray-400">
        {stepper.map((label, index) => (
          <li key={label} className="flex items-center gap-2">
            <span className="flex h-5 w-5 items-center justify-center rounded-full bg-gray-200 text-xs font-semibold text-gray-700 dark:bg-gray-800 dark:text-gray-300">
              {index + 1}
            </span>
            {label}
            {index < stepper.length - 1 && (
              <span aria-hidden="true" className="text-gray-300 dark:text-gray-700">
                →
              </span>
            )}
          </li>
        ))}
      </ol>

      <Step number={1} title={t("guide.request.title")}>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("guide.request.body")}</p>

        <div className="rounded-lg border border-accent/30 bg-accent-wash p-4 dark:border-accent/40">
          <p className="text-sm font-medium text-accent-ink">{t("guide.request.tipTitle")}</p>
          <p className="mt-1 text-sm leading-6 text-gray-700 dark:text-gray-300">{t("guide.request.tipBody")}</p>
        </div>

        <LinkedInArchiveDiagram labels={archiveDiagramLabels(tDiagram)} />

        <a
          href={LINKEDIN_DATA_EXPORT_URL}
          target="_blank"
          rel="noopener noreferrer"
          className={`self-start ${buttonClassName("primary")}`}
        >
          {t("guide.request.cta")} ↗
        </a>
      </Step>

      <Step number={2} title={t("guide.download.title")}>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("guide.download.body")}</p>

        <div
          role="note"
          className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-amber-900 dark:border-amber-900/60 dark:bg-amber-950/40 dark:text-amber-100"
        >
          <p className="text-sm font-medium">{t("guide.download.warningTitle")}</p>
          <p className="mt-1 text-sm leading-6 opacity-90">{t("guide.download.warningBody")}</p>
        </div>
      </Step>

      <Step number={3} title={t("guide.upload.title")}>
        <LinkedInImportUploader />
      </Step>

      <p className="text-sm text-gray-500 dark:text-gray-400">
        <Link href="/help/import" className="text-accent-ink underline">
          {t("helpLink")}
        </Link>
      </p>
    </div>
  );
}
