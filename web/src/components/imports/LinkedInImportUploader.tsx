"use client";

import { useRef, useState } from "react";
import { useTranslations } from "next-intl";
import type { ImportSummaryResponse } from "@/types/api";
import { importsApi } from "@/lib/api/imports";
import { ApiError } from "@/lib/api/httpClient";
import { Button } from "@/components/ui/Button";

type Status = "idle" | "uploading" | "done" | "error";

export function LinkedInImportUploader() {
  const t = useTranslations("imports.linkedin");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [status, setStatus] = useState<Status>("idle");
  const [fileName, setFileName] = useState<string | null>(null);
  const [summary, setSummary] = useState<ImportSummaryResponse | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    setFileName(file.name);
    setSummary(null);
    setErrorMessage(null);
    setStatus("uploading");

    try {
      const result = await importsApi.uploadLinkedInZip(file);
      setSummary(result);
      setStatus("done");
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : t("uploadError"));
      setStatus("error");
    } finally {
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
    }
  };

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
          disabled={status === "uploading"}
          className="text-sm text-gray-700 file:mr-3 file:rounded-md file:border-0 file:bg-blue-600 file:px-4 file:py-2 file:text-sm file:font-medium file:text-white hover:file:bg-blue-700 dark:text-gray-300"
        />
        {status === "uploading" && <span className="text-sm text-gray-500 dark:text-gray-400">{t("uploading", { fileName: fileName ?? "" })}</span>}
      </div>

      {status === "error" && errorMessage && <p className="text-sm text-red-600 dark:text-red-400">{errorMessage}</p>}

      {status === "done" && summary && (
        <div className="flex flex-col gap-3 rounded-md border border-gray-100 bg-gray-50 p-4 dark:border-gray-800 dark:bg-gray-800">
          <p className="text-sm font-medium text-gray-900 dark:text-gray-100">{t("resultTitle")}</p>
          <dl className="grid grid-cols-3 gap-4 text-sm">
            <div>
              <dt className="text-gray-500 dark:text-gray-400">{t("newApplications")}</dt>
              <dd className="text-lg font-semibold text-gray-900 dark:text-gray-100">{summary.newApplications}</dd>
            </div>
            <div>
              <dt className="text-gray-500 dark:text-gray-400">{t("duplicateRecords")}</dt>
              <dd className="text-lg font-semibold text-gray-900 dark:text-gray-100">{summary.duplicateRecords}</dd>
            </div>
            <div>
              <dt className="text-gray-500 dark:text-gray-400">{t("invalidRecords")}</dt>
              <dd className="text-lg font-semibold text-gray-900 dark:text-gray-100">{summary.invalidRecords}</dd>
            </div>
          </dl>

          {summary.errors.length > 0 && (
            <div className="mt-2">
              <p className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("rowErrors")}</p>
              <ul className="mt-1 max-h-48 overflow-y-auto text-sm text-red-600 dark:text-red-400">
                {summary.errors.map((rowError) => (
                  <li key={rowError.rowNumber}>
                    {t("rowErrorLine", { rowNumber: rowError.rowNumber, message: rowError.errorMessage })}
                  </li>
                ))}
              </ul>
            </div>
          )}

          <Button
            variant="secondary"
            className="self-start"
            onClick={() => {
              setStatus("idle");
              setSummary(null);
            }}
          >
            {t("uploadAnother")}
          </Button>
        </div>
      )}
    </div>
  );
}
