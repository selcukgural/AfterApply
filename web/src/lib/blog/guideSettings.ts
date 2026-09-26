/** What the guide page has always shown under an article, and what the API accepts (2026-09-26). */
export const MAX_RELATED_GUIDES = 2;

/**
 * The related list after one picker slot changed. Emptying a slot pulls the ones after it up, so
 * the list the API receives never has a hole and the order on the page is the order on screen;
 * picking a guide already in the other slot swaps the two rather than sending it twice (the API
 * refuses a repeat).
 */
export function setRelatedAt(current: readonly string[], index: number, id: string | null): string[] {
  const next = [...current];
  if (id === null) {
    next.splice(index, 1);
    return next;
  }
  const existing = next.indexOf(id);
  if (existing === index) return next;
  if (existing !== -1 && index < next.length) {
    [next[index], next[existing]] = [next[existing], next[index]];
    return next;
  }
  if (existing !== -1) next.splice(existing, 1);
  if (index < next.length) next[index] = id;
  else next.push(id);
  return next.slice(0, MAX_RELATED_GUIDES);
}
