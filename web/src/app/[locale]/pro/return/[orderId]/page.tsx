"use client";

import { useEffect } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useParams, useSearchParams } from "next/navigation";
import { ProgressRing } from "@/components/pro/ProgressRing";

/**
 * Where PayTR sends the browser after the payment page finishes (`merchant_ok_url` /
 * `merchant_fail_url`). PayTR's docs do not promise whether that navigation replaces the top
 * window or only the frame our checkout page embeds, so this page copes with both: framed, it
 * breaks out to the top window (allowed — the top is our own origin); top-level, it simply goes
 * on. Either way the destination is the result page, which reads the order from the API.
 * Nothing is confirmed or cancelled here — PayTR's notification is the only thing that decides,
 * and it posts no data to this URL.
 */
export default function PayTrReturnPage() {
  const t = useTranslations("common");
  const locale = useLocale();
  const params = useParams<{ orderId: string }>();
  const searchParams = useSearchParams();

  useEffect(() => {
    const outcome = searchParams.get("outcome") === "fail" ? "fail" : "ok";
    const orderId = /^[0-9a-f-]{36}$/i.test(params.orderId) ? params.orderId : "";
    const target = `${window.location.origin}/${locale}/pro/orders/${orderId}?outcome=${outcome}`;
    try {
      if (window.top && window.top !== window.self) {
        window.top.location.replace(target);
        return;
      }
    } catch {
      // A cross-origin top that refuses us: fall through and navigate ourselves.
    }
    window.location.replace(target);
  }, [locale, params.orderId, searchParams]);

  // Seen for a moment at most, top-level or inside the frame; the same waiting mark as the
  // result page, so the hand-over reads as one continuous wait rather than two screens.
  return (
    <main className="flex min-h-screen items-center justify-center p-6">
      <div
        className="flex w-full max-w-md flex-col items-center gap-3 rounded-xl border border-gray-200 bg-white px-6 py-7 text-center dark:border-gray-800 dark:bg-gray-900"
        role="status"
        aria-live="polite"
      >
        <ProgressRing size={40} spinning />
        <p className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("returningFromPayment")}</p>
        <p className="text-sm text-gray-700 dark:text-gray-300">{t("returningFromPaymentNote")}</p>
      </div>
    </main>
  );
}
