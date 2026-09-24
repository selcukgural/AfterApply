"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { buttonClassName } from "@/components/ui/Button";
import type { ContributionTile, ContributionTileKind } from "@/lib/contributions/quotaTiles";

/**
 * The header of "My contributions" (2026-09-24, canvas variant C): a tile per kind from `sm` up, a
 * list of 56px rows below it. The review tile carries the primary button, the others the outline
 * one. A full kind stays in place, dashed and not a link, so the header never reflows when a quota
 * fills. What goes in `tiles` (and in what order) is buildContributionTiles' call.
 */
const ICONS: Record<ContributionTileKind, React.ReactNode> = {
  review: <path d="M12 3.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.9z" />,
  salary: (
    <>
      <rect x="3" y="6" width="18" height="13" rx="2" />
      <path d="M3 10h18M16 15h2" />
    </>
  ),
  experience: <path d="M5 5h14a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1h-8l-4 3v-3H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z" />,
};

// Fixed class strings so Tailwind sees them; the row spreads over however many kinds are on.
const GRID_COLUMNS: Record<number, string> = { 1: "sm:grid-cols-1", 2: "sm:grid-cols-2", 3: "sm:grid-cols-3" };

function KindIcon({ kind, full }: { kind: ContributionTileKind; full: boolean }) {
  return (
    <span
      className={`inline-flex size-8 shrink-0 items-center justify-center rounded-lg ${
        full ? "bg-muted-wash text-muted-ink" : "bg-accent-wash text-accent-ink"
      }`}
    >
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
        {ICONS[kind]}
      </svg>
    </span>
  );
}

function PlusIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
      <path d="M12 5v14M5 12h14" />
    </svg>
  );
}

export function ContributionQuotaTiles({ tiles }: { tiles: ContributionTile[] }) {
  const t = useTranslations("companyReviews.mine.actions");
  if (tiles.length === 0) {
    return null;
  }

  return (
    <>
      <div className={`hidden gap-3 sm:grid ${GRID_COLUMNS[tiles.length] ?? "sm:grid-cols-3"}`}>
        {tiles.map((tile) => {
          const body = (
            <>
              <span className="flex items-center gap-2.5">
                <KindIcon kind={tile.kind} full={tile.full} />
                <span className="grow text-sm font-semibold text-gray-900 dark:text-gray-100">{t(`${tile.kind}.label`)}</span>
                <span className="text-xs tabular-nums text-gray-600 dark:text-gray-400">{t("count", { used: tile.used, limit: tile.limit })}</span>
              </span>
              <span className="block h-1 rounded-full bg-track" aria-hidden="true">
                <span className={`block h-1 rounded-full ${tile.full ? "bg-muted" : "bg-accent"}`} style={{ width: `${tile.percent}%` }} />
              </span>
            </>
          );
          if (tile.full) {
            return (
              <div key={tile.kind} className="flex flex-col gap-2.5 rounded-xl border border-dashed border-gray-300 p-4 dark:border-gray-700">
                {body}
                <span className="flex h-8 items-center text-[13px] text-gray-500 dark:text-gray-400">{t("full")}</span>
              </div>
            );
          }
          return (
            <Link
              key={tile.kind}
              href={tile.href}
              className="group flex flex-col gap-2.5 rounded-xl border border-gray-200 bg-white p-4 transition-colors hover:border-accent dark:border-gray-800 dark:bg-gray-900 dark:hover:border-accent"
            >
              {body}
              <span
                className={buttonClassName(
                  tile.kind === "review" ? "primary" : "outline",
                  "inline-flex h-8 items-center gap-1.5 self-start",
                )}
              >
                <PlusIcon />
                {t(`${tile.kind}.cta`)}
              </span>
            </Link>
          );
        })}
      </div>

      <ul className="flex flex-col overflow-hidden rounded-xl border border-gray-200 bg-white sm:hidden dark:border-gray-800 dark:bg-gray-900">
        {tiles.map((tile) => {
          const body = (
            <>
              <KindIcon kind={tile.kind} full={tile.full} />
              <span className="flex grow flex-col">
                <span className={`text-sm font-medium ${tile.full ? "text-gray-500 dark:text-gray-400" : "text-gray-900 dark:text-gray-100"}`}>
                  {t(`${tile.kind}.ctaLong`)}
                </span>
                <span className="text-xs text-gray-600 dark:text-gray-400">
                  {tile.full
                    ? t("rowFull", { label: t(`${tile.kind}.label`), used: tile.used, limit: tile.limit })
                    : t("rowUsed", { label: t(`${tile.kind}.label`), used: tile.used, limit: tile.limit })}
                </span>
              </span>
            </>
          );
          return (
            <li key={tile.kind} className="border-b border-gray-200 last:border-b-0 dark:border-gray-800">
              {tile.full ? (
                <div className="flex min-h-14 items-center gap-3 px-3.5">{body}</div>
              ) : (
                <Link href={tile.href} className="flex min-h-14 items-center gap-3 px-3.5 hover:bg-accent-wash">
                  {body}
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0 text-gray-400 dark:text-gray-500" aria-hidden="true">
                    <path d="M9 6l6 6-6 6" />
                  </svg>
                </Link>
              )}
            </li>
          );
        })}
      </ul>
    </>
  );
}
