"use client";

import { useRef, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { importsApi } from "@/lib/api/imports";
import { ApiError } from "@/lib/api/httpClient";
import { Button } from "@/components/ui/Button";
import { useImportProgress } from "@/hooks/useImportProgress";
import {
  formatFileSize,
  inspectLinkedInExportFile,
  type LinkedInExportFileProblem,
} from "@/lib/imports/linkedInExportFile";

type UploadPhase = "idle" | "uploading" | "uploadError";

export function LinkedInImportUploader() {
  const t = useTranslations("imports.linkedin");
  const locale = useLocale();
  const fileInputRef = useRef<HTMLInputElement>(null);
  // dragenter/dragleave also fire for every child the pointer crosses, so a boolean flag flickers
  // the highlight off mid-drag. Counting enters against leaves is the standard fix.
  const dragDepth = useRef(0);

  const [uploadPhase, setUploadPhase] = useState<UploadPhase>("idle");
  const [isDragging, setIsDragging] = useState(false);
  const [pickedFile, setPickedFile] = useState<{ name: string; size: number } | null>(null);
  const [fileProblem, setFileProblem] = useState<LinkedInExportFileProblem | null>(null);
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

  const isBusy = displayPhase === "uploading" || displayPhase === "processing";

  const handleFile = async (file: File) => {
    setPickedFile({ name: file.name, size: file.size });
    setBatchId(null);
    setUploadErrorMessage(null);

    const problem = inspectLinkedInExportFile(file);
    setFileProblem(problem);
    if (problem) {
      // Answered here rather than by the server: the reason is already known from the file name,
      // and a wrong file should not cost an upload of it.
      setUploadPhase("idle");
      return;
    }

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

  const handleInputChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      await handleFile(file);
    }
  };

  const handleDrop = async (event: React.DragEvent<HTMLLabelElement>) => {
    event.preventDefault();
    dragDepth.current = 0;
    setIsDragging(false);

    if (isBusy) {
      return;
    }

    const file = event.dataTransfer.files?.[0];
    if (file) {
      await handleFile(file);
    }
  };

  const resetToIdle = () => {
    setUploadPhase("idle");
    setBatchId(null);
    setUploadErrorMessage(null);
    setFileProblem(null);
    setPickedFile(null);
  };

  const percentage =
    progress?.totalRows && progress.totalRows > 0
      ? Math.min(100, Math.round((progress.processedRows / progress.totalRows) * 100))
      : null;

  // Both a rejected file and a rejected upload leave the user in the same spot: something about
  // the archive is wrong and the fix is one of three things. Offer the list once, for either.
  const showTroubleshooting = fileProblem !== null || displayPhase === "uploadError" || displayPhase === "failed";

  return (
    <div className="flex flex-col gap-4">
      {/* No heading of its own: the step this sits in already carries one. */}
      <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("description")}</p>

      <label
        onDragEnter={(event) => {
          event.preventDefault();
          dragDepth.current += 1;
          if (!isBusy) {
            setIsDragging(true);
          }
        }}
        onDragOver={(event) => event.preventDefault()}
        onDragLeave={(event) => {
          event.preventDefault();
          dragDepth.current = Math.max(0, dragDepth.current - 1);
          if (dragDepth.current === 0) {
            setIsDragging(false);
          }
        }}
        onDrop={handleDrop}
        className={`flex cursor-pointer flex-col items-center gap-2 rounded-xl border-2 border-dashed px-6 py-10 text-center transition-colors focus-within:ring-2 focus-within:ring-accent focus-within:ring-offset-2 dark:focus-within:ring-offset-gray-900 ${
          isDragging
            ? "border-accent bg-accent-wash"
            : "border-gray-300 bg-gray-50 hover:border-accent hover:bg-accent-wash/40 dark:border-gray-700 dark:bg-gray-900/60"
        } ${isBusy ? "pointer-events-none opacity-60" : ""}`}
      >
        <input
          ref={fileInputRef}
          type="file"
          accept=".zip,application/zip"
          onChange={handleInputChange}
          disabled={isBusy}
          className="sr-only"
        />
        <svg
          viewBox="0 0 24 24"
          className="h-8 w-8 text-gray-400 dark:text-gray-500"
          fill="none"
          stroke="currentColor"
          strokeWidth={1.75}
          aria-hidden="true"
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="M12 16.5V4.5m0 0L7.5 9M12 4.5L16.5 9" />
          <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 15v2.25A2.25 2.25 0 006 19.5h12a2.25 2.25 0 002.25-2.25V15" />
        </svg>
        <span className="text-sm font-medium text-gray-900 dark:text-gray-100">
          {isDragging ? t("dropzone.dragging") : t("dropzone.idle")}
        </span>
        <span className="text-sm text-accent-ink underline">{t("dropzone.browse")}</span>
        <span className="text-xs text-gray-500 dark:text-gray-400">{t("dropzone.hint")}</span>
      </label>

      {pickedFile && (
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("selectedFile", { fileName: pickedFile.name, size: formatFileSize(pickedFile.size, locale) })}
        </p>
      )}

      {fileProblem && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {t(`problem.${fileProblem}`)}
        </p>
      )}

      {displayPhase === "uploading" && (
        <p className="text-sm text-gray-500 dark:text-gray-400">{t("uploading", { fileName: pickedFile?.name ?? "" })}</p>
      )}

      {displayPhase === "processing" && (
        <div className="flex flex-col gap-2">
          <p className="text-sm text-gray-500 dark:text-gray-400">
            {t("processing", { fileName: pickedFile?.name ?? "" })}
          </p>
          <div className="h-2 w-full overflow-hidden rounded-full bg-gray-100 dark:bg-gray-800">
            <div
              className={`h-full rounded-full bg-accent transition-all duration-300 ${percentage === null ? "w-1/3 animate-pulse" : ""}`}
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
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {uploadErrorMessage}
        </p>
      )}

      {displayPhase === "failed" && (
        <div className="flex flex-col gap-2">
          <p role="alert" className="text-sm text-red-600 dark:text-red-400">
            {progress?.errorMessage ?? t("uploadError")}
          </p>
          <Button variant="secondary" className="self-start" onClick={resetToIdle}>
            {t("uploadAnother")}
          </Button>
        </div>
      )}

      {showTroubleshooting && (
        <details className="rounded-lg border border-gray-200 p-4 dark:border-gray-800">
          <summary className="cursor-pointer text-sm font-medium text-gray-700 dark:text-gray-300">
            {t("troubleshoot.title")}
          </summary>
          <ul className="mt-3 flex list-disc flex-col gap-2 pl-5 text-sm leading-6 text-gray-600 dark:text-gray-400">
            <li>{t("troubleshoot.unzipped")}</li>
            <li>{t("troubleshoot.missingJobs")}</li>
            <li>{t("troubleshoot.noApplications")}</li>
          </ul>
        </details>
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
