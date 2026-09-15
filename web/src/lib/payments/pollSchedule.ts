/**
 * How often the result page asks the API whether PayTR's notification has landed. The
 * notification normally arrives within seconds of the redirect, so the first ninety seconds poll
 * briskly; after that the bank is genuinely slow (or the notification is being retried) and a
 * slower cadence for up to ten minutes is enough — the user is told to expect an e-mail either way.
 */
export const FAST_INTERVAL_MS = 2_000;
export const SLOW_INTERVAL_MS = 10_000;
export const FAST_PHASE_MS = 90_000;
export const GIVE_UP_MS = 10 * 60_000;

/** The delay before the next poll given how long we have been waiting, or null to stop. */
export function nextPollDelay(elapsedMs: number): number | null {
  if (elapsedMs >= GIVE_UP_MS) {
    return null;
  }
  return elapsedMs < FAST_PHASE_MS ? FAST_INTERVAL_MS : SLOW_INTERVAL_MS;
}

/** Whether a "still waiting" explanation should replace the spinner copy. */
export function isWaitingLong(elapsedMs: number): boolean {
  return elapsedMs >= FAST_PHASE_MS;
}
