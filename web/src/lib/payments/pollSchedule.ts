/**
 * How the result page waits for PayTR's notification after a checkout: fast at first (the
 * bank usually answers within seconds), slowly after ninety seconds (a 3-D Secure retry, a slow
 * acquirer), and not at all after ten minutes — by then the e-mail is the channel, and the page
 * says so.
 */
export const FAST_INTERVAL_MS = 2_000;
export const SLOW_INTERVAL_MS = 10_000;
export const FAST_PHASE_MS = 90_000;
export const GIVE_UP_MS = 10 * 60_000;

export type PendingPhase = "fast" | "slow" | "gaveUp";

export function pendingPhase(elapsedMs: number): PendingPhase {
  if (elapsedMs >= GIVE_UP_MS) {
    return "gaveUp";
  }
  return elapsedMs < FAST_PHASE_MS ? "fast" : "slow";
}

export function nextPollDelay(elapsedMs: number): number | null {
  switch (pendingPhase(elapsedMs)) {
    case "fast":
      return FAST_INTERVAL_MS;
    case "slow":
      return SLOW_INTERVAL_MS;
    default:
      return null;
  }
}

export function isWaitingLong(elapsedMs: number): boolean {
  return elapsedMs >= FAST_PHASE_MS;
}

/** `m:ss` for the elapsed-time readout; hours are not a case this page reaches. */
export function formatElapsed(elapsedMs: number): string {
  const totalSeconds = Math.max(0, Math.floor(elapsedMs / 1000));
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

/** The order id as the page shows it: enough to match against an e-mail, not the whole GUID. */
export function shortOrderId(orderId: string): string {
  return orderId.length > 13 ? `${orderId.slice(0, 8)}…${orderId.slice(-5)}` : orderId;
}
