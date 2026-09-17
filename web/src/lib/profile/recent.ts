/** How many of a person's contributions the profile page shows before "See all". */
export const RECENT_CONTRIBUTIONS = 3;

/**
 * The newest entries first, at most `limit` of them. The "mine" endpoints return everything the
 * person owns in the server's order; the profile page only wants the latest few, so it sorts on
 * the submission date itself rather than trusting the wire order.
 */
export function takeRecent<T extends { submittedAt: string }>(items: readonly T[], limit = RECENT_CONTRIBUTIONS): T[] {
  return [...items].sort((a, b) => Date.parse(b.submittedAt) - Date.parse(a.submittedAt)).slice(0, Math.max(0, limit));
}
