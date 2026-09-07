"use client";

import { useEffect, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { cvDocumentsApi } from "@/lib/api/cvDocuments";

type PreviewState = "loading" | "ready" | "failed";

/** Width the first page is rendered at, in CSS pixels. Sized against the panel it sits in (the
 *  content column is 1024px, of which the list rail takes 320) rather than against the window, so
 *  the page reads as a page rather than as a stamp. Anything larger only costs memory for a
 *  thumbnail nobody reads at that size. */
const PREVIEW_WIDTH = 420;

/**
 * Renders the first page of a stored PDF onto a canvas, in the browser.
 *
 * Mount this with `key={documentId}`: switching documents is a remount, so the loading state
 * starts fresh without the effect having to reset it (React 19 flags a synchronous setState in an
 * effect body, and rightly — resetting state on a prop change is what a key is for).
 *
 * Deliberately client-side. Producing this image on the server would mean a native PDF renderer in
 * the API container (and LibreOffice for anything else), a place to keep the thumbnails, and a
 * rule for regenerating them — for a picture whose whole job is to answer "which CV is this?".
 * Rendering to a canvas also needs no CSP change: the worker is bundled, so it loads from our own
 * origin, and nothing about the PDF is ever executed as a document on our origin the way an
 * <iframe> or <embed> preview would be.
 */
export function CvPdfPreview({ documentId }: { documentId: string }) {
  const t = useTranslations("cv.preview");
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [state, setState] = useState<PreviewState>("loading");

  useEffect(() => {
    // Guards every await below: the user can click through the list faster than a page renders,
    // and a late render must not paint over the canvas the current selection owns.
    let cancelled = false;
    let renderTask: { cancel: () => void } | null = null;
    // Owns the worker and the parsed document; destroying it is what releases both. (The document
    // proxy itself has no destroy() in pdf.js 6 — the loading task is the handle.)
    let loadingTask: { destroy: () => Promise<void> } | null = null;

    const render = async () => {
      // Dynamic import: pdf.js is a large dependency, and it has no business in the bundle of
      // every other page in the app.
      const [pdfjs, blob] = await Promise.all([
        import("pdfjs-dist"),
        cvDocumentsApi.content(documentId),
      ]);

      if (cancelled) {
        return;
      }

      // The worker resolves to a file served from our own origin, which is what keeps this inside
      // the app's existing `default-src 'self'` policy — a CDN copy would be blocked outright.
      pdfjs.GlobalWorkerOptions.workerSrc = new URL(
        "pdfjs-dist/build/pdf.worker.min.mjs",
        import.meta.url,
      ).toString();

      const task = pdfjs.getDocument({ data: await blob.arrayBuffer() });
      loadingTask = task;
      const document_ = await task.promise;

      if (cancelled) {
        return;
      }

      const page = await document_.getPage(1);
      const canvas = canvasRef.current;

      if (cancelled || !canvas) {
        return;
      }

      const unscaled = page.getViewport({ scale: 1 });
      // Rendered at the device's own pixel density, then sized back down in CSS — otherwise the
      // page is visibly soft on any retina screen.
      const pixelRatio = window.devicePixelRatio || 1;
      const viewport = page.getViewport({ scale: (PREVIEW_WIDTH / unscaled.width) * pixelRatio });

      canvas.width = Math.floor(viewport.width);
      canvas.height = Math.floor(viewport.height);
      canvas.style.width = `${PREVIEW_WIDTH}px`;
      canvas.style.height = `${Math.floor(viewport.height / pixelRatio)}px`;

      renderTask = page.render({ canvas, viewport });
      await (renderTask as unknown as { promise: Promise<void> }).promise;

      if (!cancelled) {
        setState("ready");
      }
    };

    render().catch(() => {
      // A PDF that pdf.js cannot parse, or a download that failed. Either way the file is still
      // downloadable — only the picture of it is missing, which is what the fallback says.
      if (!cancelled) {
        setState("failed");
      }
    });

    return () => {
      cancelled = true;
      renderTask?.cancel();
      // Floating on purpose: a cleanup function cannot await, and there is nothing to do with the
      // result. Swallowing the rejection keeps a teardown race from surfacing as an unhandled one.
      void loadingTask?.destroy().catch(() => {});
    };
  }, [documentId]);

  return (
    <div className="flex flex-col items-center gap-3 rounded-md bg-muted-wash p-6">
      <div
        className="flex items-center justify-center rounded-sm border border-gray-200 bg-white dark:border-gray-700"
        style={{ width: PREVIEW_WIDTH, minHeight: state === "ready" ? undefined : 424 }}
      >
        {/* Kept mounted while loading so the ref exists by the time the render task needs it. */}
        <canvas ref={canvasRef} className={state === "ready" ? "block" : "hidden"} aria-label={t("firstPageAlt")} />

        {state === "loading" && <div className="aa-skeleton h-[424px] w-full rounded-sm" aria-hidden="true" />}

        {state === "failed" && (
          <p className="px-6 py-10 text-center text-sm leading-6 text-gray-500 dark:text-gray-400">
            {t("failed")}
          </p>
        )}
      </div>

      {state === "ready" && (
        <p className="text-xs text-gray-500 dark:text-gray-400">{t("firstPageOnly")}</p>
      )}
    </div>
  );
}
