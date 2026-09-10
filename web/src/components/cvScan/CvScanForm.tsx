"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useMutation } from "@tanstack/react-query";
import { Link } from "@/i18n/navigation";
import { cvScanApi } from "@/lib/api/cvScan";
import { ApiError } from "@/lib/api/httpClient";
import { CV_SCAN_ACCEPT, inspectScanFile, type CvScanFileProblem } from "@/lib/cvScan/findings";
import { trackSiteTraffic } from "@/lib/analytics/siteTraffic";
import { Button } from "@/components/ui/Button";
import { Checkbox } from "@/components/ui/Checkbox";
import { useClientConfig } from "@/hooks/useClientConfig";
import { CvScanResult } from "@/components/cvScan/CvScanResult";
import type { CvScanResponse } from "@/types/api";

export function CvScanForm() {
  const t = useTranslations("cvScan");
  const locale = useLocale();
  const { config } = useClientConfig();
  const fileInputRef = useRef<HTMLInputElement>(null);
  // dragenter/dragleave fire for every child the pointer crosses, so a boolean flickers the
  // highlight off mid-drag. Counting enters against leaves is the fix the CV manager and the
  // LinkedIn import dropzone both use.
  const dragDepth = useRef(0);
  // When the form was first shown. Sent with the scan: a submission that arrives within a second
  // and a half of the page appearing did not have a person behind it. It is a client-set number
  // and the server treats it as one — see CvScanRequest.ElapsedMilliseconds.
  const openedAt = useRef(0);

  const [file, setFile] = useState<File | null>(null);
  const [fileProblem, setFileProblem] = useState<CvScanFileProblem | null>(null);
  const [consentAccepted, setConsentAccepted] = useState(false);
  // The optional second consent, and unticked like the first: this one decides whether the text of
  // the CV is sent to a model at all. Ticking it changes nothing about the score.
  const [contentNotesRequested, setContentNotesRequested] = useState(false);
  const [consentError, setConsentError] = useState(false);
  const [isDragging, setIsDragging] = useState(false);
  const [website, setWebsite] = useState("");
  const [result, setResult] = useState<CvScanResponse | null>(null);

  // Set in an effect rather than at construction: the clock is impure, and reading it during
  // render is both a lint error and a lie — the number that matters is when the form reached the
  // visitor's screen.
  useEffect(() => {
    openedAt.current = Date.now();
  }, []);

  const scan = useMutation({
    mutationFn: () =>
      cvScanApi.scan({
        file: file!,
        consentAccepted,
        contentNotesRequested: contentNotesRequested && config.cvScan.contentNotesAvailable,
        locale,
        website,
        elapsedMs: Date.now() - openedAt.current,
      }),
    onSuccess: (response) => {
      setResult(response);
      // The funnel step the whole surface is measured on: a page view says someone arrived, this
      // says they got the thing the page promised. Anonymous and best-effort, like every other
      // event — see lib/analytics/siteTraffic.
      trackSiteTraffic("cv_scan_completed");
    },
  });

  const pick = (picked: File | undefined) => {
    if (!picked) return;

    const problem = inspectScanFile(picked);
    setFileProblem(problem);
    setFile(problem ? null : picked);
    scan.reset();
  };

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();

    if (!file) {
      setFileProblem((current) => current ?? "unsupportedType");
      return;
    }

    if (!consentAccepted) {
      setConsentError(true);
      return;
    }

    setConsentError(false);
    scan.mutate();
  };

  const reset = () => {
    setResult(null);
    setFile(null);
    setFileProblem(null);
    // Consent is per scan, never carried over: a box left ticked from the previous file is not
    // explicit consent for this one. Both boxes, for the same reason.
    setConsentAccepted(false);
    setContentNotesRequested(false);
    openedAt.current = Date.now();
    scan.reset();
  };

  const submitError = scan.isError
    ? scan.error instanceof ApiError
      ? scan.error.status === 429
        ? t("errors.tooMany")
        : (scan.error.message ?? t("errors.generic"))
      : t("errors.generic")
    : null;

  if (result) {
    return <CvScanResult result={result} onReset={reset} />;
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-5" noValidate>
      <div
        onDragEnter={(event) => {
          event.preventDefault();
          dragDepth.current += 1;
          setIsDragging(true);
        }}
        onDragOver={(event) => event.preventDefault()}
        onDragLeave={() => {
          dragDepth.current -= 1;
          if (dragDepth.current <= 0) setIsDragging(false);
        }}
        onDrop={(event) => {
          event.preventDefault();
          dragDepth.current = 0;
          setIsDragging(false);
          pick(event.dataTransfer.files[0]);
        }}
        className={`flex flex-col items-center gap-3 rounded-xl border-2 border-dashed p-8 text-center transition-colors ${
          isDragging
            ? "border-blue-500 bg-blue-50 dark:bg-blue-950/30"
            : "border-gray-300 bg-white dark:border-gray-700 dark:bg-gray-900"
        }`}
      >
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("form.dragHint")}{" "}
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            className="font-medium text-blue-600 underline underline-offset-2 hover:text-blue-700 dark:text-blue-400"
          >
            {t("form.choose")}
          </button>
        </p>
        <p className="text-xs text-gray-500 dark:text-gray-400">{t("form.dropHint")}</p>

        <input
          ref={fileInputRef}
          type="file"
          accept={CV_SCAN_ACCEPT}
          className="hidden"
          onChange={(event) => pick(event.target.files?.[0])}
        />

        {file ? (
          <p className="text-sm text-gray-900 dark:text-gray-100">
            {t("form.selected")}: <span className="font-medium">{file.name}</span>{" "}
            <button
              type="button"
              onClick={() => {
                setFile(null);
                if (fileInputRef.current) fileInputRef.current.value = "";
              }}
              className="text-blue-600 underline underline-offset-2 dark:text-blue-400"
            >
              {t("form.remove")}
            </button>
          </p>
        ) : null}
      </div>

      {fileProblem ? (
        <p className="text-sm text-red-600 dark:text-red-400">{t(`errors.${fileProblem}`)}</p>
      ) : null}

      {/* Unticked on every visit and after every scan. A pre-ticked box is not valid explicit
          consent, and consent here is given per file — the same rule the stored-CV upload follows. */}
      <Checkbox
        id="cv-scan-consent"
        checked={consentAccepted}
        onChange={(event) => {
          setConsentAccepted(event.target.checked);
          setConsentError(false);
        }}
        error={consentError ? t("errors.consentRequired") : undefined}
        label={
          <span className="flex flex-col gap-1">
            <span>{t("form.consentLabel")}</span>
            <span className="text-xs text-gray-500 dark:text-gray-400">
              {t.rich("form.consentDetail", {
                privacy: (chunks) => (
                  <Link href="/privacy" className="underline underline-offset-2">
                    {chunks}
                  </Link>
                ),
              })}
            </span>
          </span>
        }
      />

      {/* Offered only where it can actually be honoured — with layer B off, a box promising notes
          would be a promise the deployment cannot keep. Optional in the real sense: leaving it
          unticked costs the reader the notes and nothing else, and the score is identical. */}
      {config.cvScan.contentNotesAvailable ? (
        <Checkbox
          id="cv-scan-content-notes"
          checked={contentNotesRequested}
          onChange={(event) => setContentNotesRequested(event.target.checked)}
          label={
            <span className="flex flex-col gap-1">
              <span>{t("form.contentNotesLabel")}</span>
              <span className="text-xs text-gray-500 dark:text-gray-400">{t("form.contentNotesDetail")}</span>
            </span>
          }
        />
      ) : null}

      {/* Honeypot. Hidden from people and from assistive technology, left in the DOM for anything
          that fills every input it finds — the same field the benchmark form carries, for the same
          reason: a CAPTCHA is a third-party script the CSP forbids. */}
      <div className="hidden" aria-hidden="true">
        <label htmlFor="cv-scan-website">Website</label>
        <input
          id="cv-scan-website"
          name="website"
          type="text"
          tabIndex={-1}
          autoComplete="off"
          value={website}
          onChange={(event) => setWebsite(event.target.value)}
        />
      </div>

      {submitError ? <p className="text-sm text-red-600 dark:text-red-400">{submitError}</p> : null}

      <div>
        <Button type="submit" disabled={scan.isPending || !file}>
          {scan.isPending ? t("form.submitting") : t("form.submit")}
        </Button>
      </div>
    </form>
  );
}
