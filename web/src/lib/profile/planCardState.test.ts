import { describe, expect, it } from "vitest";
import { resolvePlanCardState } from "./planCardState";

const on = { jobSources: { enabled: true }, payments: { enabled: true } } as const;
const off = { jobSources: { enabled: true }, payments: { enabled: false } } as const;

describe("resolvePlanCardState", () => {
  it("shows a running period as active whether or not the checkout is on", () => {
    const plan = { isActive: true, activeUntil: "2026-10-16T00:00:00Z" };
    expect(resolvePlanCardState(plan, on)).toEqual({ state: "active", canBuy: true });
    expect(resolvePlanCardState(plan, off)).toEqual({ state: "active", canBuy: false });
  });

  it("keeps the end date of a period that ran out, and offers to buy again only when it can", () => {
    const plan = { isActive: false, activeUntil: "2026-08-16T00:00:00Z" };
    expect(resolvePlanCardState(plan, on)).toEqual({ state: "expired", canBuy: true });
    expect(resolvePlanCardState(plan, off)).toEqual({ state: "expired", canBuy: false });
  });

  it("offers Go Pro to a free account only while there is a checkout", () => {
    const plan = { isActive: false, activeUntil: null };
    expect(resolvePlanCardState(plan, on).state).toBe("free-can-buy");
    expect(resolvePlanCardState(plan, off).state).toBe("free");
  });

  it("reads as free while the plan has not loaded yet", () => {
    expect(resolvePlanCardState(undefined, off).state).toBe("free");
  });
});
