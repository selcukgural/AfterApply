"use client";

import { useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { importsApi } from "@/lib/api/imports";
import { ApiError } from "@/lib/api/httpClient";
import { Button } from "@/components/ui/Button";
import { useImportProgress } from "@/hooks/useImportProgress";

type UploadPhase = "idle" | "uploading" | "uploadError";

export function LinkedInImportUploader() {
  const t = useTranslations("imports.linkedin");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [uploadPhase, setUploadPhase] = useState<UploadPhase>("idle");
  const [fileName, setFileName] = useState<string | null>(null);
  const [batchId, setBatchId] = useState<string | null>(null);
  const [uploadErrorMessage, setUploadErrorMessage] = useState<string | null>(null);

  const progress = useImportProgress(batchId);

  // Once a batch exists, its live status (via SignalR, resynced over GET on connect) drives
  // the UI directly — no separate "processing/done/failed" state to keep in sync by hand.
  const displayPhase =
    uploadPhase === "uploading"
      ? "uploading"
      : uploadPhase === "uploadError"
        ? "uploadError"
        : !batchId
          ? "idle"
          : !progress || progress.status === "Pending" || progress.status === "Processing"
            ? "processing"
            : progress.status === "Completed"
              ? "done"
              : "failed";

  const handleFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    setFileName(file.name);
    setBatchId(null);
    setUploadErrorMessage(null);
    setUploadPhase("uploading");

    try {
      const accepted = await importsApi.uploadLinkedInZip(file);
      setBatchId(accepted.id);
      setUploadPhase("idle");
    } catch (error) {
      setUploadErrorMessage(error instanceof ApiError ? error.message : t("uploadError"));
      setUploadPhase("uploadError");
    } finally {
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
    }
  };

  const resetToIdle = () => {
    setUploadPhase("idle");
    setBatchId(null);
    setUploadErrorMessage(null);
  };

  const isBusy = displayPhase === "uploading" || displayPhase === "processing";
  const percentage =
    progress?.totalRows && progress.totalRows > 0
      ? Math.min(100, Math.round((progress.processedRows / progress.totalRows) * 100))
      : null;

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
      <div>
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{t("description")}</p>
      </div>

      <div className="flex items-center gap-3">
        <input
          ref={fileInputRef}
          type="file"
          accept=".zip"
          onChange={handleFileChange}
          disabled={isBusy}
          className="text-sm text-gray-700 file:mr-3 file:rounded-md file:border-0 file:bg-blue-600 file:px-4 file:py-2 file:text-sm file:font-medium file:text-white hover:file:bg-blue-700 dark:text-gray-300"
        />
      </div>

      {displayPhase === "uploading" && (
        <p className="text-sm text-gray-500 dark:text-gray-400">{t("uploading", { fileName: fileName ?? "" })}</p>
      )}

      {displayPhase === "processing" && (
        <div className="flex flex-col gap-2">
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("processing", { fileName: fileName ?? "" })}</p>
          <div className="h-2 w-full overflow-hidden rounded-full bg-gray-100 dark:bg-gray-800">
            <div
              className={`h-full rounded-full bg-blue-600 transition-all duration-300 ${percentage === null ? "w-1/3 animate-pulse" : ""}`}
              style={percentage !== null ? { width: `${percentage}%` } : undefined}
            />
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {progress?.totalRows
              ? t("processingRows", { processed: progress.processedRows, total: progress.totalRows })
              : t("processingRowsUnknownTotal", { processed: progress?.processedRows ?? 0 })}
          </p>
        </div>
      )}

      {displayPhase === "uploadError" && uploadErrorMessage && (
        <p className="text-sm text-red-600 dark:text-red-400">{uploadErrorMessage}</p>
      )}

      {displayPhase === "failed" && (
        <div className="flex flex-col gap-2">
          <p className="text-sm text-red-600 dark:text-red-400">{progress?.errorMessage ?? t("uploadError")}</p>
          <Button variant="secondary" className="self-start" onClick={resetToIdle}>
            {t("uploadAnother")}
          </Button>
        </div>
      )}

      {displayPhase === "done" && progress && (
        <div className="flex flex-col gap-3 rounded-md border border-gray-100 bg-gray-50 p-4 dark:border-gray-800 dark:bg-gray-800">
          <p className="text-sm font-medium text-gray-900 dark:text-gray-100">{t("resultTitle")}</p>
          <dl className="grid grid-cols-3 gap-4 text-sm">
            <div>
              <dt className="text-gray-500 dark:text-gray-400">{t("newApplications")}</dt>
              <dd className="text-lg font-semibold text-gray-900 dark:text-gray-100">{progress.newApplications}</dd>
            </div>
            <div>
              <dt className="text-gray-500 dark:text-gray-400">{t("duplicateRecords")}</dt>
              <dd className="text-lg font-semibold text-gray-900 dark:text-gray-100">{progress.duplicateRecords}</dd>
            </div>
            <div>
              <dt className="text-gray-500 dark:text-gray-400">{t("invalidRecords")}</dt>
              <dd className="text-lg font-semibold text-gray-900 dark:text-gray-100">{progress.invalidRecords}</dd>
            </div>
          </dl>

          {progress.errors.length > 0 && (
            <div className="mt-2">
              <p className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("rowErrors")}</p>
              <ul className="mt-1 max-h-48 overflow-y-auto text-sm text-red-600 dark:text-red-400">
                {progress.errors.map((rowError) => (
                  <li key={rowError.rowNumber}>
                    {t("rowErrorLine", { rowNumber: rowError.rowNumber, message: rowError.errorMessage })}
                  </li>
                ))}
              </ul>
            </div>
          )}

          <Button variant="secondary" className="self-start" onClick={resetToIdle}>
            {t("uploadAnother")}
          </Button>
        </div>
      )}
    </div>
  );
}
