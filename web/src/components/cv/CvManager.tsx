"use client";

import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import type { CvDocumentResponse } from "@/types/api";
import { cvDocumentsApi, saveBlob } from "@/lib/api/cvDocuments";
import { ApiError } from "@/lib/api/httpClient";
import { formatFileSize } from "@/lib/imports/linkedInExportFile";
import { CV_FILE_ACCEPT, canPreview, cvFormatLabel, inspectCvFile, type CvFileProblem } from "@/lib/cv/cvFile";
import { Button } from "@/components/ui/Button";
import { Checkbox } from "@/components/ui/Checkbox";
import { Link } from "@/i18n/navigation";
import { CvPdfPreview } from "@/components/cv/CvPdfPreview";

const CV_QUERY_KEY = ["cvDocuments"] as const;

function DocumentIcon({ className }: { className: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={1.75} aria-hidden="true">
      <path strokeLinecap="round" strokeLinejoin="round" d="M14 3.5H7.5A1.5 1.5 0 006 5v14a1.5 1.5 0 001.5 1.5h9A1.5 1.5 0 0018 19V7.5L14 3.5z" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M14 3.5V7.5H18" />
    </svg>
  );
}

export function CvManager() {
  const t = useTranslations("cv");
  const tCommon = useTranslations("common");
  const locale = useLocale();
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  // dragenter/dragleave also fire for every child the pointer crosses, so a boolean flag flickers
  // the highlight off mid-drag. Counting enters against leaves is the standard fix — same as the
  // LinkedIn import dropzone.
  const dragDepth = useRef(0);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [fileProblem, setFileProblem] = useState<CvFileProblem | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [consentError, setConsentError] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  // Starts unticked on every visit, and is cleared again after each upload. A pre-ticked box is
  // not valid explicit consent, and consent here is given per file rather than once per account —
  // the same rule the CV/OpenAI consent followed before that feature was removed
  // (DECISIONS.md 2026-09-01).
  const [consentAccepted, setConsentAccepted] = useState(false);

  const { data, isLoading } = useQuery({ queryKey: CV_QUERY_KEY, queryFn: () => cvDocumentsApi.list() });

  const items = data?.items ?? [];
  const maxCount = data?.maxCount ?? 0;
  const isFull = maxCount > 0 && items.length >= maxCount;

  // The server orders the default first, then newest — so "no explicit selection" lands on the
  // CV the user most likely wants to look at. Also recovers when the selected one is deleted.
  const selected = items.find((item) => item.id === selectedId) ?? items[0] ?? null;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: CV_QUERY_KEY });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => cvDocumentsApi.upload(file, consentAccepted),
    onSuccess: async (created) => {
      setSelectedId(created.id);
      // Consent covers the file that was just stored, not the next one.
      setConsentAccepted(false);
      await invalidate();
    },
    onError: (error) => {
      // The server scopes a refused upload to the field that caused it, so a missing consent lands
      // next to the checkbox rather than in the page-level error line.
      const fieldErrors =
        error instanceof ApiError && error.body && typeof error.body === "object" && "errors" in error.body
          ? (error.body as { errors: Record<string, string[]> }).errors
          : undefined;

      if (fieldErrors?.consentAccepted?.[0]) {
        setConsentError(fieldErrors.consentAccepted[0]);
        return;
      }

      setErrorMessage(error instanceof ApiError ? error.message : t("uploadError"));
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => cvDocumentsApi.remove(id),
    onSuccess: async () => {
      setSelectedId(null);
      setConfirmingDelete(false);
      await invalidate();
    },
    onError: (error) => setErrorMessage(error instanceof ApiError ? error.message : t("deleteError")),
  });

  const defaultMutation = useMutation({
    mutationFn: (id: string) => cvDocumentsApi.setDefault(id),
    onSuccess: () => invalidate(),
    onError: (error) => setErrorMessage(error instanceof ApiError ? error.message : t("defaultError")),
  });

  const [isDownloading, setIsDownloading] = useState(false);

  const handleDownload = async (document_: CvDocumentResponse) => {
    setErrorMessage(null);
    setIsDownloading(true);
    try {
      saveBlob(await cvDocumentsApi.content(document_.id), document_.fileName);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : t("downloadError"));
    } finally {
      setIsDownloading(false);
    }
  };

  const handleFile = (file: File) => {
    setErrorMessage(null);
    setConsentError(null);

    const problem = inspectCvFile(file);
    setFileProblem(problem);
    if (problem) {
      // Answered here rather than by the server: the reason is already known from the file itself,
      // and a wrong file should not cost an upload of it.
      return;
    }

    uploadMutation.mutate(file);
  };

  const isBusy = uploadMutation.isPending || deleteMutation.isPending;
  // Consent gates the upload controls themselves, so the box cannot be an afterthought clicked
  // past on the way to a file picker.
  const canUpload = consentAccepted && !isBusy && !isFull;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </div>

      {errorMessage && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {errorMessage}
        </p>
      )}

      <div className="flex flex-col gap-6 md:flex-row md:items-start">
        {/* Left rail: upload, quota, the list itself. */}
        <div className="flex w-full flex-col gap-3 md:w-80 md:shrink-0">
          <input
            ref={fileInputRef}
            type="file"
            accept={CV_FILE_ACCEPT}
            className="sr-only"
            disabled={!canUpload}
            onChange={(event) => {
              const file = event.target.files?.[0];
              if (file) {
                handleFile(file);
              }
              // Cleared so picking the same file twice in a row still fires a change event.
              event.target.value = "";
            }}
          />

          <Checkbox
            id="cvConsentAccepted"
            checked={consentAccepted}
            onChange={(event) => {
              setConsentAccepted(event.target.checked);
              setConsentError(null);
            }}
            error={consentError ?? undefined}
            label={
              <>
                {t("consent.before")}{" "}
                <Link href="/privacy#cv-storage" target="_blank" className="text-blue-600 hover:underline dark:text-blue-400">
                  {t("consent.link")}
                </Link>{" "}
                {t("consent.after")}
              </>
            }
          />

          <p className="text-xs leading-5 text-gray-500 dark:text-gray-400">{t("consent.sensitiveWarning")}</p>

          <Button
            onClick={() => fileInputRef.current?.click()}
            disabled={!canUpload}
            className="flex items-center justify-center gap-2"
          >
            <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
              <path strokeLinecap="round" d="M12 5v14M5 12h14" />
            </svg>
            {uploadMutation.isPending ? t("uploading") : t("upload")}
          </Button>

          <div className="flex items-center justify-between gap-2">
            <span className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-gray-400">
              {t("listHeading")}
            </span>
            <span className="text-xs text-gray-500 dark:text-gray-400">
              {t("quota", { used: items.length, max: maxCount })}
            </span>
          </div>

          {isFull && <p className="text-xs text-gray-500 dark:text-gray-400">{t("quotaFull", { max: maxCount })}</p>}

          {fileProblem && (
            <p role="alert" className="text-sm text-red-600 dark:text-red-400">
              {t(`problem.${fileProblem}`)}
            </p>
          )}

          {isLoading ? (
            <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {items.map((item) => {
                const isSelected = item.id === selected?.id;
                return (
                  <li key={item.id}>
                    <button
                      type="button"
                      onClick={() => {
                        setSelectedId(item.id);
                        // A pending confirmation belongs to the file it was opened on; moving to
                        // another one must not leave a primed delete button pointing at it.
                        setConfirmingDelete(false);
                        setErrorMessage(null);
                      }}
                      aria-current={isSelected}
                      className={`flex w-full items-center gap-2.5 rounded-lg border p-3 text-left transition-colors ${
                        isSelected
                          ? "border-accent bg-accent-wash"
                          : "border-gray-200 bg-white hover:border-gray-300 dark:border-gray-800 dark:bg-gray-900 dark:hover:border-gray-700"
                      }`}
                    >
                      <span
                        className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-md ${
                          isSelected ? "bg-white dark:bg-gray-900" : "bg-muted-wash"
                        }`}
                      >
                        <DocumentIcon className={isSelected ? "h-4 w-4 text-accent-ink" : "h-4 w-4 text-muted-ink"} />
                      </span>
                      <span className="flex min-w-0 flex-col gap-0.5">
                        <span className="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
                          {item.fileName}
                        </span>
                        <span className="flex items-center gap-1.5">
                          {item.isDefault && (
                            <span className="rounded bg-good-wash px-1.5 py-0.5 text-[11px] font-medium text-good-ink">
                              {t("defaultBadge")}
                            </span>
                          )}
                          <span className={`text-xs ${isSelected ? "text-accent-ink" : "text-gray-500 dark:text-gray-400"}`}>
                            {cvFormatLabel(item.format)} · {formatFileSize(item.sizeBytes, locale)}
                          </span>
                        </span>
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}

          {/* The dropzone doubles as the list's empty state — there is nothing else to say when a
              user has no CVs yet, and an "upload your first CV" panel would say it twice. */}
          <label
            onDragEnter={(event) => {
              event.preventDefault();
              dragDepth.current += 1;
              if (canUpload) {
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
            onDrop={(event) => {
              event.preventDefault();
              dragDepth.current = 0;
              setIsDragging(false);
              if (!canUpload) {
                return;
              }
              const file = event.dataTransfer.files?.[0];
              if (file) {
                handleFile(file);
              }
            }}
            onClick={() => fileInputRef.current?.click()}
            className={`flex cursor-pointer flex-col items-center gap-1 rounded-lg border-2 border-dashed px-3 py-5 text-center transition-colors ${
              isDragging
                ? "border-accent bg-accent-wash"
                : "border-gray-300 bg-gray-50 hover:border-accent dark:border-gray-700 dark:bg-gray-900/60"
            } ${canUpload ? "" : "pointer-events-none opacity-60"}`}
          >
            <span className="text-sm text-gray-600 dark:text-gray-400">{t("dropzone.idle")}</span>
            <span className="text-xs text-gray-500 dark:text-gray-500">{t("dropzone.hint")}</span>
          </label>
        </div>

        {/* Right panel: the selected CV. */}
        <div className="min-w-0 flex-1">
          {selected ? (
            <div className="flex flex-col gap-4 rounded-lg border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                <div className="flex min-w-0 flex-col gap-1">
                  <p className="truncate text-base font-semibold text-gray-900 dark:text-gray-100">
                    {selected.fileName}
                  </p>
                  <p className="text-sm text-gray-500 dark:text-gray-400">
                    {t("meta", {
                      format: cvFormatLabel(selected.format),
                      size: formatFileSize(selected.sizeBytes, locale),
                      date: new Date(selected.uploadedAt).toLocaleDateString(locale),
                    })}
                  </p>
                </div>
                <div className="flex shrink-0 gap-2">
                  <Button variant="secondary" onClick={() => handleDownload(selected)} disabled={isDownloading}>
                    {isDownloading ? t("downloading") : t("download")}
                  </Button>
                  <Button variant="danger" onClick={() => setConfirmingDelete(true)} disabled={isBusy}>
                    {tCommon("delete")}
                  </Button>
                </div>
              </div>

              {confirmingDelete && (
                <div className="flex flex-col gap-3 rounded-lg bg-crit-wash p-4 sm:flex-row sm:items-center sm:justify-between">
                  <p className="text-sm leading-5 text-crit-ink">{t("deleteConfirm")}</p>
                  <div className="flex shrink-0 gap-2">
                    <Button variant="secondary" onClick={() => setConfirmingDelete(false)}>
                      {tCommon("cancel")}
                    </Button>
                    <Button
                      variant="danger"
                      onClick={() => deleteMutation.mutate(selected.id)}
                      disabled={deleteMutation.isPending}
                    >
                      {t("deleteConfirmAction")}
                    </Button>
                  </div>
                </div>
              )}

              {canPreview(selected.format) ? (
                /* Keyed so switching CVs remounts the preview rather than reusing its state. */
                <CvPdfPreview key={selected.id} documentId={selected.id} />
              ) : (
                <div className="flex flex-col items-center gap-3 rounded-md bg-muted-wash px-6 py-12 text-center">
                  <DocumentIcon className="h-10 w-10 text-muted-ink" />
                  <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">
                    {t("preview.unsupported", { format: cvFormatLabel(selected.format) })}
                  </p>
                </div>
              )}

              <div className="flex items-center justify-between gap-4 border-t border-gray-200 pt-4 dark:border-gray-800">
                <div className="flex min-w-0 flex-col gap-0.5">
                  <p className="text-sm font-medium text-gray-900 dark:text-gray-100">{t("default.title")}</p>
                  <p className="text-sm text-gray-500 dark:text-gray-400">
                    {t("default.description")}{" "}
                    {t("default.usedBy", { count: selected.usedByApplicationCount })}
                  </p>
                </div>
                <Button
                  variant={selected.isDefault ? "secondary" : "primary"}
                  onClick={() => defaultMutation.mutate(selected.id)}
                  disabled={selected.isDefault || defaultMutation.isPending}
                  className="shrink-0"
                >
                  {selected.isDefault ? t("default.isDefault") : t("default.makeDefault")}
                </Button>
              </div>
            </div>
          ) : (
            !isLoading && (
              <div className="rounded-lg border border-dashed border-gray-300 p-10 text-center dark:border-gray-700">
                <p className="text-sm leading-6 text-gray-500 dark:text-gray-400">{t("empty")}</p>
              </div>
            )
          )}
        </div>
      </div>
    </div>
  );
}
