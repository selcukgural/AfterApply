"use client";

import { useLocale } from "next-intl";
import type { ReactNode } from "react";
import { Link } from "@/i18n/navigation";
import { useClientConfig } from "@/hooks/useClientConfig";
import { salaryMarketPath } from "@/lib/salaryMarket/path";

/**
 * A sentence pointing at the survey salary pages, from places that lack figures of their own (a
 * company's empty salary tab, the offer comparison). Nothing while the SalaryMarket flag is off —
 * the pages ship dark, and a link to a 404 is worse than none.
 */
export function SalaryMarketLink({ text, label, className }: { text: ReactNode; label: string; className?: string }) {
  const locale = useLocale();
  const { config } = useClientConfig();
  if (config.salaryMarket?.enabled !== true) return null;

  return (
    <p className={className ?? "text-sm text-gray-600 dark:text-gray-400"}>
      {text}{" "}
      <Link href={salaryMarketPath(locale)} className="font-medium text-accent-ink underline-offset-2 hover:underline">
        {label}
      </Link>
    </p>
  );
}
