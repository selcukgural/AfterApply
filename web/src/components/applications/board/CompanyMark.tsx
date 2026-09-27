"use client";

import { useQuery } from "@tanstack/react-query";
import { boardApi } from "@/lib/api/board";
import { companyInitials, companyTint } from "@/lib/board/board";

/** Six soft tints, each a wash with its own darker ink, so the letters stay readable in both
 *  themes. Indexed by companyTint: a company keeps its colour on every card and every visit. */
const TINTS = [
  "bg-accent-wash text-accent-ink",
  "bg-good-wash text-good-ink",
  "bg-warn-wash text-warn-ink",
  "bg-muted-wash text-muted-ink",
  "bg-purple-100 text-purple-800 dark:bg-purple-900/40 dark:text-purple-200",
  "bg-pink-100 text-pink-800 dark:bg-pink-900/40 dark:text-pink-200",
];

/**
 * The company's logo as an object URL, fetched once per company and shared by every card that
 * shows it. Kept for the session: logos change rarely, and a board redraw must not refetch them.
 */
function useCompanyLogo(companyId: string, enabled: boolean): string | undefined {
  const query = useQuery({
    queryKey: ["company-logo", companyId],
    queryFn: async () => URL.createObjectURL(await boardApi.companyLogo(companyId)),
    enabled,
    staleTime: Infinity,
    gcTime: Infinity,
    retry: false,
  });
  return query.data;
}

/**
 * The small square beside a company's name on a board card: its logo when the API has one
 * (DECISIONS.md 2026-09-27), otherwise — and while it loads, or if it fails — its initials on a
 * tint. Decorative either way: the name is always written next to it.
 */
export function CompanyMark({
  companyId,
  companyName,
  hasLogo = false,
  size = "sm",
}: {
  companyId: string;
  companyName: string;
  hasLogo?: boolean;
  size?: "sm" | "md";
}) {
  const logo = useCompanyLogo(companyId, hasLogo);
  const box = size === "md" ? "h-8 w-8 rounded-lg text-xs" : "h-5 w-5 rounded-[5px] text-[9px]";

  if (logo) {
    return (
      // A plain <img>: the source is a blob: URL of bytes we stored, which next/image cannot optimise.
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={logo}
        alt=""
        aria-hidden="true"
        className={`shrink-0 bg-white object-contain ring-1 ring-inset ring-black/5 dark:ring-white/10 ${box}`}
      />
    );
  }

  return (
    <span
      aria-hidden="true"
      className={`inline-flex shrink-0 items-center justify-center font-bold ring-1 ring-inset ring-black/5 dark:ring-white/10 ${box} ${TINTS[companyTint(companyId)]}`}
    >
      {companyInitials(companyName)}
    </span>
  );
}
