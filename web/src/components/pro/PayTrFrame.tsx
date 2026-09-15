"use client";

import { useEffect, useState } from "react";
import Script from "next/script";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/Button";

declare global {
  interface Window {
    // PayTR's build of iframe-resizer's parent-side script (https://www.paytr.com/js/iframeResizer.min.js).
    iFrameResize?: (options: Record<string, unknown>, selector: string) => void;
  }
}

const IFRAME_ID = "paytriframe";
// PayTR's iframeResizer.min.js, self-hosted (public/vendor) so script-src stays 'self'.
const RESIZER_SRC = "/vendor/paytr-iframeResizer.min.js";

interface PayTrFrameProps {
  iframeUrl: string;
  expiresAt: string;
  busy: boolean;
  onCancel: () => void;
  onExpired: () => void;
}

/**
 * Step 2 of the checkout: PayTR's payment page in an iframe, as their docs prescribe (their
 * resizer script — served from our own origin, see RESIZER_SRC — `id="paytriframe"`,
 * `scrolling="no"`, width 100%). The card form, 3-D
 * Secure and the bank's answers all happen inside; nothing card-related ever touches our page.
 *
 * Fault tolerance: if the resizer script does not load (blocked, offline), the frame falls back
 * to a fixed height with its own scrollbar so paying is still possible. The countdown mirrors
 * PayTR's `timeout_limit`; when it runs out the frame is replaced by a "start again" prompt —
 * the token is single-use and the order is closed by the expiry job.
 */
export function PayTrFrame({ iframeUrl, expiresAt, busy, onCancel, onExpired }: PayTrFrameProps) {
  const t = useTranslations("payments.checkout");
  const [resizerFailed, setResizerFailed] = useState(false);
  const [confirmingCancel, setConfirmingCancel] = useState(false);
  const [secondsLeft, setSecondsLeft] = useState(() => remainingSeconds(expiresAt));

  useEffect(() => {
    const timer = window.setInterval(() => {
      const left = remainingSeconds(expiresAt);
      setSecondsLeft(left);
      if (left <= 0) {
        window.clearInterval(timer);
        onExpired();
      }
    }, 1000);
    return () => window.clearInterval(timer);
  }, [expiresAt, onExpired]);

  const minutes = Math.floor(Math.max(secondsLeft, 0) / 60);
  const seconds = Math.max(secondsLeft, 0) % 60;

  return (
    <div className="flex flex-col gap-3">
      <Script
        src={RESIZER_SRC}
        strategy="afterInteractive"
        onLoad={() => window.iFrameResize?.({}, `#${IFRAME_ID}`)}
        onError={() => setResizerFailed(true)}
      />
      <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-gray-600 dark:text-gray-400">
        <span>{t("timeLeft", { minutes, seconds: seconds.toString().padStart(2, "0") })}</span>
        {confirmingCancel ? (
          <span className="flex flex-wrap items-center gap-2">
            <span>{t("cancelConfirm")}</span>
            <Button type="button" variant="danger" onClick={onCancel} disabled={busy}>
              {t("cancelYes")}
            </Button>
            <Button type="button" variant="secondary" onClick={() => setConfirmingCancel(false)} disabled={busy}>
              {t("cancelNo")}
            </Button>
          </span>
        ) : (
          <Button type="button" variant="secondary" onClick={() => setConfirmingCancel(true)} disabled={busy}>
            {t("cancel")}
          </Button>
        )}
      </div>
      <div className="overflow-hidden rounded-lg border border-gray-200 bg-white dark:border-gray-800">
        <iframe
          id={IFRAME_ID}
          src={iframeUrl}
          title={t("frameTitle")}
          scrolling={resizerFailed ? "yes" : "no"}
          className="w-full"
          style={{ minHeight: resizerFailed ? 760 : 480, border: 0 }}
        />
      </div>
      <p className="text-xs text-gray-500 dark:text-gray-400">{t("frameNote")}</p>
    </div>
  );
}

function remainingSeconds(expiresAt: string): number {
  return Math.round((new Date(expiresAt).getTime() - Date.now()) / 1000);
}
