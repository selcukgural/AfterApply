"use client";

import { useEffect } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useParams, useSearchParams } from "next/navigation";

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

  return (
    <main className="flex min-h-screen items-center justify-center p-6">
      <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>
    </main>
  );
}
