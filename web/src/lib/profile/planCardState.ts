import type { ClientConfigResponse, UserPlanResponse } from "@/types/api";
import { canSeeProNav } from "@/lib/payments/proNav";

/**
 * What the profile page's plan line says. Decided from two things: whether the person's Pro
 * period is running (the entitlement, readable regardless of flags), and whether there is
 * anything to buy right now (canSeeProNav — checkout and weekly jobs both on). The states keep
 * the two apart so that a person with a manual grant sees "Pro until …" even while the checkout
 * is off, and nobody is offered a button that leads to a 404.
 *
 * - `active`       Pro is running; show the end date and the link to the plan page.
 * - `expired`      a period ended (or was revoked); say when, and offer to buy again if possible.
 * - `free-can-buy` never had Pro, checkout is on: the free badge and "Go Pro".
 * - `free`         never had Pro, nothing to buy: the free badge alone — no "coming soon" promise.
 */
export type PlanCardState = "active" | "expired" | "free-can-buy" | "free";

export function resolvePlanCardState(
  plan: UserPlanResponse | undefined,
  config: Pick<ClientConfigResponse, "jobSources" | "payments">,
): { state: PlanCardState; canBuy: boolean } {
  const canBuy = canSeeProNav(config);
  if (plan?.isActive) return { state: "active", canBuy };
  if (plan?.activeUntil) return { state: "expired", canBuy };
  return { state: canBuy ? "free-can-buy" : "free", canBuy };
}
