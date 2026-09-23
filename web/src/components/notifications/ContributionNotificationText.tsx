"use client";

import { useTranslations } from "next-intl";
import type { ContributionNotificationResponse } from "@/types/api";

/**
 * "3 people found your review of <b>Trendyol</b> helpful." — the one sentence both the bell's panel
 * and the Notifications page print for a contribution row. Canvas variant C1: how many, never who.
 */
export function ContributionNotificationText({ contribution: c }: { contribution: ContributionNotificationResponse }) {
  const t = useTranslations("notifications.contribution");
  return (
    <>
      {t.rich(c.type, {
        count: c.count,
        company: c.companyName ?? "",
        post: c.blogPostTitle ?? "",
        b: (chunks) => <strong className="font-semibold">{chunks}</strong>,
      })}
    </>
  );
}

/** The thumbs-up the contribution rows carry, where the Gmail rows carry an envelope. */
export function HelpfulIcon({ className = "h-4 w-4" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M7 10v12" />
      <path d="M15 5.88 14 10h5.83a2 2 0 0 1 1.92 2.56l-2.33 8A2 2 0 0 1 17.5 22H4a2 2 0 0 1-2-2v-8a2 2 0 0 1 2-2h2.76a2 2 0 0 0 1.79-1.11L12 2a3.13 3.13 0 0 1 3 3.88Z" />
    </svg>
  );
}

export function MailIcon({ className = "h-4 w-4" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect x="2" y="4" width="20" height="16" rx="2" />
      <path d="m22 7-10 5L2 7" />
    </svg>
  );
}
