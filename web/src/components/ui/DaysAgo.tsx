"use client";

import { daysAgo } from "@/lib/applications/daysAgo";

interface DaysAgoProps {
  iso: string;
  locale: string;
  className?: string;
}

/** A relative day ("12 days ago") with the exact date on hover and for assistive tech. */
export function DaysAgo({ iso, locale, className }: DaysAgoProps) {
  const exact = new Date(iso).toLocaleDateString(locale);
  return (
    <time dateTime={iso} title={exact} className={className}>
      {daysAgo(iso, locale)}
      <span className="sr-only"> ({exact})</span>
    </time>
  );
}
