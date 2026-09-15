import type { ClientConfigResponse } from "@/types/api";

export const PRO_NAV_HREF = "/pro";

/**
 * Whether to show the "Pro" entry (menu link, gate button, settings section). Both server flags
 * have to be on: the weekly job matching is what Pro buys (JobSources:Enabled), and the checkout
 * itself must exist (PayTr:Enabled with secrets present). Either off means every /pro route
 * answers 404 or redirects, so a link would only lead to a dead page.
 */
export function canSeeProNav(config: Pick<ClientConfigResponse, "jobSources" | "payments">): boolean {
  return config.jobSources?.enabled === true && config.payments?.enabled === true;
}
