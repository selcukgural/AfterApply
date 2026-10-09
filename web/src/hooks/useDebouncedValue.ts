"use client";

import { useEffect, useState } from "react";

/** `value`, once it has stopped changing for `delayMs` — so a lookup runs on the finished paste or
 *  word, not on every keystroke on the way there. */
export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}
