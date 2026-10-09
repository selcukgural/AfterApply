import type { ApplicationSummaryResponse } from "@/types/api";

/** How many of the company's other applications the quick card lists under the one it shows. */
export const QUICK_CARD_OTHERS = 3;

/** The company's other applications, newest first as fetched, without the one the card is about. */
export function otherApplicationsAtCompany(
  items: ApplicationSummaryResponse[],
  currentId: string,
): ApplicationSummaryResponse[] {
  return items.filter((item) => item.id !== currentId).slice(0, QUICK_CARD_OTHERS);
}

/**
 * Which application the applications search should open as a card on a phone (canvas "İnce
 * dokunuşlar — Paket 5", 2B), or null. Only when a search is active and every row it found is at
 * one company — that is the "someone from X is calling" search; a search that spans companies is
 * an ordinary list. Of that company's rows, the latest application, whatever the list's sort.
 */
export function quickCardTarget(search: string, items: ApplicationSummaryResponse[]): string | null {
  if (search.trim() === "" || items.length === 0) return null;
  const companyId = items[0].companyId;
  if (items.some((item) => item.companyId !== companyId)) return null;
  return items.reduce((latest, item) => (Date.parse(item.appliedAt) > Date.parse(latest.appliedAt) ? item : latest)).id;
}

/** The next highlighted row when the arrow keys move through `count` results, wrapping round. */
export function moveActive(active: number, count: number, key: "ArrowUp" | "ArrowDown"): number {
  if (count === 0) return 0;
  return key === "ArrowDown" ? (active + 1) % count : (active - 1 + count) % count;
}

/** Ctrl+K, or ⌘K on a Mac: opens the quick find from anywhere in the signed-in app. */
export function isQuickFindShortcut(event: Pick<KeyboardEvent, "key" | "ctrlKey" | "metaKey" | "altKey" | "shiftKey">): boolean {
  return event.key.toLowerCase() === "k" && (event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey;
}

/** Opens the quick find from a control on the page (the hint beside the applications search). */
export const QUICK_FIND_OPEN_EVENT = "ekariyerim:quick-find-open";

/** Whether to name the shortcut ⌘K rather than Ctrl K. Only ever a label; both keys work everywhere. */
export function prefersCommandKey(platform: string): boolean {
  return /Mac|iPhone|iPad/.test(platform);
}
