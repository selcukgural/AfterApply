/** The two sides of the contribute page. */
export type ContributeTab = "review" | "salary";

/** `?tab=` as typed into a URL: anything that is not "salary" opens the review side. */
export function parseContributeTab(raw: string | null | undefined): ContributeTab {
  return raw === "salary" ? "salary" : "review";
}

export interface ContributionContext {
  /** The caller already has a review of this company (any status). */
  ownReview: boolean;
  /** How many salary entries the caller already has for this company. */
  ownSalaryCount: number;
  reviewQuotaLeft: number;
  salaryQuotaLeft: number;
  reviewsEnabled: boolean;
  salariesEnabled: boolean;
}

/**
 * Where the page goes after a save (design canvas 3B, 2026-09-16): the other side of the same
 * company, with a thank-you banner and the form ready — unless that side has nothing left to
 * offer. A review is one per company, so after a salary the review side is offered only when
 * there is none yet; salaries are one per job title, so after a review the salary side is
 * offered whenever quota remains, even with entries already there. Null means "nothing to
 * invite to" and the page falls back to the author's own list, as before the cross-invite.
 */
export function nextSideAfterSave(saved: ContributeTab, context: ContributionContext): ContributeTab | null {
  if (saved === "salary") {
    return context.reviewsEnabled && !context.ownReview && context.reviewQuotaLeft > 0 ? "review" : null;
  }
  return context.salariesEnabled && context.salaryQuotaLeft > 0 ? "salary" : null;
}

/** The URL of a contribute side, with the company when one is chosen. */
export function contributeHref(tab: ContributeTab, companySlug?: string | null): string {
  return companySlug ? `/contribute?tab=${tab}&company=${encodeURIComponent(companySlug)}` : `/contribute?tab=${tab}`;
}
