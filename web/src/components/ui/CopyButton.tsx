"use client";

import { useTranslations } from "next-intl";
import { useCopy } from "@/hooks/useCopy";

interface CopyButtonProps {
  /** Already gated by the caller: only a value that is also shown as a link gets a copy button. */
  value: string;
  /** What is being copied, for screen readers ("Copy HR email address"). */
  label: string;
}

/** A small icon button beside a value people paste elsewhere: an HR address, a posting link. */
export function CopyButton({ value, label }: CopyButtonProps) {
  const t = useTranslations("share");
  const { copied, copy } = useCopy();

  return (
    <button
      type="button"
      onClick={() => void copy(value)}
      title={copied ? t("copied") : label}
      aria-label={label}
      className="inline-flex size-6 shrink-0 items-center justify-center rounded-full text-gray-400 transition-colors hover:bg-gray-100 hover:text-accent dark:hover:bg-gray-800"
    >
      {copied ? (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true" className="size-3.5 text-emerald-600">
          <path d="m5 12 5 5 9-10" />
        </svg>
      ) : (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true" className="size-3.5">
          <rect x="9" y="9" width="11" height="11" rx="2" />
          <path d="M5 15V6a2 2 0 0 1 2-2h9" />
        </svg>
      )}
      <span className="sr-only" aria-live="polite">
        {copied ? t("copied") : ""}
      </span>
    </button>
  );
}
