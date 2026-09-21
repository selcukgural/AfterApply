import type { SectorResponseRateRow } from "@/types/api";

/**
 * The visible rows first, sorted by application count so the best-evidenced sector leads, then
 * the hidden ones in the server's (enum) order. A hidden row carries no count to sort by — that
 * is the point of it being hidden — so its position says nothing about its size.
 */
export function splitSectorRows(rows: readonly SectorResponseRateRow[]): {
  visible: SectorResponseRateRow[];
  hidden: SectorResponseRateRow[];
} {
  const visible = rows.filter((row) => row.figures !== null).sort((a, b) => b.figures!.applications - a.figures!.applications);
  const hidden = rows.filter((row) => row.figures === null);
  return { visible, hidden };
}
