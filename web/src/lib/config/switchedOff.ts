import type { ClientConfigResponse } from "@/types/api";

/** The features whose way in is withdrawn once the server says they are off. */
export interface SwitchedOff {
  /** The guide lives on the blog's routes (Blog flag): /guide answers 404 while it is off. */
  guide: boolean;
  /** /cv-tarama and the scan of stored CVs (CvScan flag). */
  cvScan: boolean;
  /** The company directory and pages (CompanyReviews flag). */
  companies: boolean;
  /** The e-mail suggestions and their counter (EmailSignals flag). */
  suggestions: boolean;
}

/**
 * Which links to take away because an admin switched a feature off (runtime flags, DECISIONS.md
 * 2026-09-27). Only an explicit `false` counts: `null` — the answer not in yet, or the API
 * unreachable — and a field an older API does not send both keep every link, which is how these
 * pages behaved before the flags could change without a deploy. The reverse rule (show only once
 * the server says on) stays with the features that ship dark: the blog link, response rates,
 * weekly jobs.
 */
export function switchedOff(config: Partial<ClientConfigResponse> | null | undefined): SwitchedOff {
  return {
    guide: config?.blog?.enabled === false,
    cvScan: config?.cvScan?.enabled === false,
    companies: config?.companyReviews?.enabled === false,
    suggestions: config?.emailSignals?.enabled === false,
  };
}

/** The link keys a switched-off feature takes with it — the keys the navbar, the signed-out header
 *  and the footer already give these destinations. */
const LINK_KEYS: Record<Exclude<keyof SwitchedOff, "suggestions">, string> = {
  guide: "guide",
  cvScan: "cvScan",
  companies: "companies",
};

/** `items` without the links to features switched off: the one rule every menu applies. */
export function withoutSwitchedOff<T extends { key: string }>(items: readonly T[], off: SwitchedOff): T[] {
  const gone = new Set(
    (Object.keys(LINK_KEYS) as (keyof typeof LINK_KEYS)[]).filter((feature) => off[feature]).map((feature) => LINK_KEYS[feature]),
  );
  return items.filter((item) => !gone.has(item.key));
}
