"use client";

import { useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import { CV_SCAN_ACCEPT, inspectScanFile, type CvScanFileProblem } from "@/lib/cvScan/findings";
import { stashScanFile } from "@/lib/cvScan/pendingScanFile";

/**
 * The hero's right-hand side: somewhere to put a CV.
 *
 * It replaced a picture of a dashboard nobody can open without an account. The trade it makes is
 * the point of the whole page — a visitor arrives with a file they already own and gets an answer
 * before being asked for anything.
 *
 * **It does not scan.** Dropping a file here stashes it and navigates; the consent box, the score
 * and everything the result screen has to say about what an ATS is all live on /cv-tarama, and
 * they stay there. That keeps three things true at once: consent is still given deliberately on
 * the page that does the reading, the funnel still has one denominator (/cv-tarama page views),
 * and the hero stays a hero rather than turning into a report.
 *
 * The file is checked here anyway — wrong format, too large — because refusing it in the hero
 * keeps the person where their context is, instead of navigating them somewhere to be told no.
 */
export function HeroCvDropzone() {
  const t = useTranslations("landing.heroScan");
  const tErrors = useTranslations("cvScan.errors");
  const router = useRouter();

  const fileInputRef = useRef<HTMLInputElement>(null);
  // dragenter/dragleave fire for every child the pointer crosses, so a boolean flickers the
  // highlight off mid-drag. Counting enters against leaves is the fix every dropzone in this app
  // uses (CvScanForm, CvManager, LinkedInImportUploader).
  const dragDepth = useRef(0);

  const [isDragging, setIsDragging] = useState(false);
  const [problem, setProblem] = useState<CvScanFileProblem | null>(null);

  const pick = (picked: File | undefined) => {
    if (!picked) return;

    const found = inspectScanFile(picked);
    setProblem(found);
    if (found) return;

    stashScanFile(picked);
    router.push("/cv-tarama");
  };

  return (
    <div className="w-full max-w-md">
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
        className={`flex flex-col items-center gap-3 rounded-xl border-2 border-dashed p-8 text-center shadow-sm transition-colors ${
          isDragging
            ? "border-accent bg-accent-wash"
            : "border-gray-300 bg-white dark:border-gray-700 dark:bg-gray-900"
        }`}
      >
        <svg
          viewBox="0 0 24 24"
          className="h-8 w-8 text-accent-ink"
          fill="none"
          stroke="currentColor"
          strokeWidth={1.75}
          aria-hidden="true"
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="M12 16.5V4.5m0 0L8.25 8.25M12 4.5l3.75 3.75" />
          <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 15v3A1.5 1.5 0 006 19.5h12a1.5 1.5 0 001.5-1.5v-3" />
        </svg>

        <p className="text-base font-medium text-gray-900 dark:text-gray-100">{t("title")}</p>

        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("dragHint")}{" "}
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            className="font-medium text-accent-ink underline underline-offset-2"
          >
            {t("choose")}
          </button>
        </p>

        <p className="text-xs text-gray-500 dark:text-gray-400">{t("formats")}</p>

        <input
          ref={fileInputRef}
          type="file"
          accept={CV_SCAN_ACCEPT}
          className="hidden"
          onChange={(event) => pick(event.target.files?.[0])}
        />
      </div>

      {/* Announced rather than merely shown: the file was refused after an action the visitor took
          with a pointer, and the refusal is the only feedback there is. */}
      <div aria-live="polite" className="min-h-5">
        {problem ? <p className="mt-2 text-sm text-red-600 dark:text-red-400">{tErrors(problem)}</p> : null}
      </div>

      <p className="mt-2 text-xs text-gray-500 dark:text-gray-400">{t("noAccount")}</p>
      {/* Says what happens next, at the moment of the drop. Without it, being moved to another page
          with the file already in the box reads as something having been decided on the visitor's
          behalf — which is the one impression this feature cannot afford to give. */}
      <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{t("nextStep")}</p>
    </div>
  );
}
