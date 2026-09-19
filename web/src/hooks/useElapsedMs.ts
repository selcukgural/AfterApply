"use client";

import { useEffect, useState } from "react";

/**
 * Milliseconds since `key` last changed, as state that advances every animation frame until
 * `totalMs` and then stops. Drives the count-up next to the CV-scan category bars.
 *
 * Two things it deliberately does: the server (and the first client render) reports `totalMs`,
 * so static HTML — the shared-score page, a crawler's view of it — already carries the final
 * figures rather than a row of zeros; and under `prefers-reduced-motion: reduce` it stays there,
 * matching the CSS side, which switches the bars' animation off under the same query.
 */
export function useElapsedMs(totalMs: number, key: string): number {
  const [elapsed, setElapsed] = useState(totalMs);

  useEffect(() => {
    // Every update is made from a frame callback, never from the effect body itself, so a change
    // of key mid-run lands on the next frame like any other tick.
    const still = window.matchMedia("(prefers-reduced-motion: reduce)").matches || totalMs <= 0;
    const start = performance.now();

    const tick = (now: number) => {
      const next = still ? totalMs : Math.min(totalMs, now - start);
      setElapsed(next);
      if (next < totalMs) frame = requestAnimationFrame(tick);
    };
    let frame = requestAnimationFrame(tick);

    return () => cancelAnimationFrame(frame);
  }, [totalMs, key]);

  return elapsed;
}
