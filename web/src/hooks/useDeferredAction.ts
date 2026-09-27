"use client";

import { useEffect, useRef, useState } from "react";

/**
 * "Done — undo?" without a server-side undo: the action is held for a few seconds and only then
 * sent. Undo simply drops it. A new action sends the held one first, and leaving the page sends it
 * too, so nothing the user did is lost by moving on — only closing the tab inside the window drops
 * it, which leaves the reminder where it was (the safe direction).
 */
export function useDeferredAction<T>(commit: (item: T) => void, delayMs = 6000) {
  const [pending, setPending] = useState<T | null>(null);
  const pendingRef = useRef<T | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const commitRef = useRef(commit);

  useEffect(() => {
    commitRef.current = commit;
  });

  const clearTimer = () => {
    if (timerRef.current !== null) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  };

  const flush = () => {
    clearTimer();
    const item = pendingRef.current;
    pendingRef.current = null;
    if (item !== null) commitRef.current(item);
  };

  const startTimer = () => {
    clearTimer();
    timerRef.current = setTimeout(() => {
      flush();
      setPending(null);
    }, delayMs);
  };

  const schedule = (item: T) => {
    flush();
    pendingRef.current = item;
    setPending(item);
    startTimer();
  };

  // While the pointer or focus is on the undo strip the clock stops: the strip closing under a
  // cursor on its way to "Undo" shifts the rows up, and the click lands on whatever moved there.
  const hold = () => {
    if (pendingRef.current !== null) clearTimer();
  };

  const release = () => {
    if (pendingRef.current !== null) startTimer();
  };

  const cancel = () => {
    clearTimer();
    pendingRef.current = null;
    setPending(null);
  };

  // Leaving the page is not an undo: send what is still held.
  useEffect(
    () => () => {
      if (timerRef.current !== null) clearTimeout(timerRef.current);
      const item = pendingRef.current;
      pendingRef.current = null;
      if (item !== null) commitRef.current(item);
    },
    [],
  );

  return { pending, schedule, cancel, hold, release };
}
