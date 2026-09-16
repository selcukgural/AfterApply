"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { ProgressRing } from "@/components/pro/ProgressRing";
import { buttonClassName } from "@/components/ui/Button";
import { formatElapsed, pendingPhase, shortOrderId } from "@/lib/payments/pollSchedule";

interface PaymentVerifyingProps {
  /** Milliseconds since the wait began — the ring's readout and what picks the phase. */
  elapsedMs: number;
  /** PayTR's `?outcome=` hint: "fail" only changes the title. */
  outcomeHint: string | null;
  orderId: string;
  planName: string;
  amount: string;
  /** When the order was last read; null before the first read. */
  lastCheckedAt: number | null;
  /** The clock the "last checked" readout is computed against (the page ticks it). */
  now: number;
  isChecking: boolean;
  onCheckNow: () => void;
}

/**
 * The screen a buyer sees between PayTR's payment page and the result: the "B — Halka"
 * direction from the 2026-09-15 design canvas. One focus — the ring with the elapsed time —
 * a sentence on where things stand, and a badge that says the one thing that matters to the
 * buyer at each phase: keep the page open (we will show the result here) for the first ten
 * minutes, then that closing it loses nothing (the result arrives by e-mail). The two are not
 * in tension: the first is advice, the second a guarantee, and the guarantee holds throughout.
 *
 * Nothing here decides anything; the order only leaves Pending through PayTR's server-to-server
 * notification. "Check now" re-reads the order, it does not confirm a payment — and it only
 * appears once the page has slowed its own polling to ten seconds (or stopped it): in the fast
 * phase the next read is at most two seconds away, and a button there only says "you should be
 * doing something", which is the opposite of the message.
 */
export function PaymentVerifying({
  elapsedMs,
  outcomeHint,
  orderId,
  planName,
  amount,
  lastCheckedAt,
  now,
  isChecking,
  onCheckNow,
}: PaymentVerifyingProps) {
  const t = useTranslations("payments.result");
  const phase = pendingPhase(elapsedMs);
  const active = phase !== "gaveUp";
  const secondsSinceCheck = lastCheckedAt === null ? null : Math.max(0, Math.floor((now - lastCheckedAt) / 1000));

  const statusText = phase === "gaveUp" ? t("pendingGaveUp") : phase === "slow" ? t("pendingLong") : t("verifying");
  const badgeTone =
    phase === "fast"
      ? "bg-accent-wash text-accent-ink"
      : phase === "slow"
        ? "bg-warn-wash text-warn-ink"
        : "bg-muted-wash text-muted-ink";

  return (
    <div className="flex flex-col gap-4" role="status" aria-live="polite">
      <div className="flex flex-col items-center gap-4 px-4 pt-8 pb-2 text-center">
        <ProgressRing size={128} spinning={active}>
          <span className="font-mono text-2xl font-medium leading-8 text-gray-900 tabular-nums dark:text-gray-100">
            {formatElapsed(elapsedMs)}
          </span>
          <span className="text-[11px] leading-4 text-gray-500 dark:text-gray-400">{t("elapsedLabel")}</span>
        </ProgressRing>

        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">
          {outcomeHint === "fail" ? t("verifyingFailTitle") : t("verifyingTitle")}
        </h1>

        <div className="flex max-w-xl flex-col items-center gap-3">
          <p className="text-sm text-gray-700 dark:text-gray-300">{statusText}</p>
          <span className={`inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-[13px] font-medium ${badgeTone}`}>
            {active && (
              <span className="inline-block h-2 w-2 rounded-full bg-current motion-safe:animate-pulse" aria-hidden="true" />
            )}
            {active ? t("keepOpen") : t("safeToClose")}
          </span>
        </div>
      </div>

      <dl className="mx-auto grid w-full max-w-xl grid-cols-2 gap-px overflow-hidden rounded-xl border border-gray-200 bg-gray-200 dark:border-gray-800 dark:bg-gray-800">
        <Detail label={t("planLabel")} value={planName} />
        <Detail label={t("amountLabel")} value={amount} />
        <Detail label={t("orderLabel")} value={shortOrderId(orderId)} mono />
        <Detail
          label={t("lastCheckedLabel")}
          value={
            !active
              ? t("checksStopped")
              : secondsSinceCheck === null
                ? "—"
                : secondsSinceCheck < 2
                  ? t("lastCheckedNow")
                  : t("lastChecked", { seconds: secondsSinceCheck })
          }
        />
      </dl>

      <div className="flex flex-wrap items-center justify-center gap-3 pt-1">
        {phase !== "fast" && (
          <button
            type="button"
            onClick={onCheckNow}
            disabled={isChecking}
            className={buttonClassName("secondary", "inline-flex items-center gap-2")}
          >
            <svg
              viewBox="0 0 24 24"
              className={`h-4 w-4 ${isChecking ? "motion-safe:animate-spin" : ""}`}
              fill="none"
              stroke="currentColor"
              strokeWidth={2}
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <path d="M20 12a8 8 0 1 1-2.34-5.66" />
              <path d="M20 4v5h-5" />
            </svg>
            {t("checkNow")}
          </button>
        )}
        <Link href="/pro" className="text-sm text-gray-500 underline dark:text-gray-400">
          {t("backToPlans")}
        </Link>
      </div>
    </div>
  );
}

function Detail({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex flex-col gap-0.5 bg-white px-4 py-3 dark:bg-gray-900">
      <dt className="text-[11px] leading-4 text-gray-500 dark:text-gray-400">{label}</dt>
      <dd className={`text-sm text-gray-900 dark:text-gray-100 ${mono ? "font-mono text-[13px]" : ""}`}>{value}</dd>
    </div>
  );
}
