/** The three sides of the contribute page, in the order the switch shows them. */
export type ContributeTab = "review" | "salary" | "experience";

export const CONTRIBUTE_TABS: readonly ContributeTab[] = ["review", "salary", "experience"];

/** `?tab=` as typed into a URL: anything that is not a known side opens the review side. */
export function parseContributeTab(raw: string | null | undefined): ContributeTab {
  return raw === "salary" || raw === "experience" ? raw : "review";
}

export interface ContributionContext {
  /** The caller already has a review of this company (any status). */
  ownReview: boolean;
  /** How many salary entries the caller already has for this company. */
  ownSalaryCount: number;
  /** The caller already rated their hiring process at this company. */
  ownExperience: boolean;
  reviewQuotaLeft: number;
  salaryQuotaLeft: number;
  experienceQuotaLeft: number;
  reviewsEnabled: boolean;
  salariesEnabled: boolean;
  experiencesEnabled: boolean;
}

/**
 * Where the page goes after a save (design canvas 3B, 2026-09-16; three sides since 2026-09-17):
 * the first other side, in switch order, that is on, has quota and the author has not used for
 * this company yet — with a thank-you banner and the form ready. A review and an experience are
 * one per company; salaries are one per occupation, so once every fresh side is taken the
 * salary side is still offered whenever quota remains, as it was before the third side. Null
 * means "nothing to invite to" and the page falls back to the author's own list.
 */
export function nextSideAfterSave(saved: ContributeTab, context: ContributionContext): ContributeTab | null {
  const fresh: Record<ContributeTab, boolean> = {
    review: context.reviewsEnabled && !context.ownReview && context.reviewQuotaLeft > 0,
    salary: context.salariesEnabled && context.ownSalaryCount === 0 && context.salaryQuotaLeft > 0,
    experience: context.experiencesEnabled && !context.ownExperience && context.experienceQuotaLeft > 0,
  };
  const next = CONTRIBUTE_TABS.find((tab) => tab !== saved && fresh[tab]);
  if (next) return next;
  return saved !== "salary" && context.salariesEnabled && context.salaryQuotaLeft > 0 ? "salary" : null;
}

/** The URL of a contribute side, with the company when one is chosen. */
export function contributeHref(tab: ContributeTab, companySlug?: string | null): string {
  return companySlug ? `/contribute?tab=${tab}&company=${encodeURIComponent(companySlug)}` : `/contribute?tab=${tab}`;
}

/** The author's own list for a side — where "skip" and a save with nothing left to invite to go. */
export function ownListHref(tab: ContributeTab): string {
  return tab === "salary" ? "/my-salaries" : tab === "experience" ? "/my-experiences" : "/my-reviews";
}
