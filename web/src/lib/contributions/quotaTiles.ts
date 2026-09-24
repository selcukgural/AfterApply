/**
 * The header of "My contributions" (2026-09-24, canvas variant C): one tile per contribution kind,
 * carrying that kind's quota and its call to action — the grey quota line and the three stacked
 * buttons it replaced said the same thing twice. A kind whose feature is off reports no quota and
 * gets no tile. A kind at its limit keeps its tile, drawn full and without a link: the quota counts
 * the rows that exist, so deleting one frees the slot and the copy can say so.
 */
export type ContributionTileKind = "review" | "salary" | "experience";

export interface QuotaLike {
  used: number;
  limit: number;
}

export interface ContributionTile {
  kind: ContributionTileKind;
  used: number;
  limit: number;
  full: boolean;
  /** 0–100, for the bar. */
  percent: number;
  href: string;
}

export function buildContributionTiles(quotas: {
  reviewQuota: QuotaLike;
  salaryQuota: QuotaLike | null;
  experienceQuota: QuotaLike | null;
}): ContributionTile[] {
  const entries: [ContributionTileKind, QuotaLike | null][] = [
    ["review", quotas.reviewQuota],
    ["salary", quotas.salaryQuota],
    ["experience", quotas.experienceQuota],
  ];
  return entries.flatMap(([kind, quota]) => {
    if (!quota) {
      return [];
    }
    // The review limit can be an admin override, even 0; never divide by it blindly.
    const full = quota.used >= quota.limit;
    const percent = quota.limit > 0 ? Math.min(100, Math.round((quota.used / quota.limit) * 100)) : 100;
    return [{ kind, used: quota.used, limit: quota.limit, full, percent, href: `/contribute?tab=${kind}` }];
  });
}
