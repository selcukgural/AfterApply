"use client";

import { useEffect, useState } from "react";

/**
 * Copy text to the clipboard and remember, for a moment, that it worked — long enough for a
 * "Copied" label to be read, short enough that the button is ready for the next copy.
 */
export function useCopy(resetAfterMs = 1500) {
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const timer = setTimeout(() => setCopied(false), resetAfterMs);
    return () => clearTimeout(timer);
  }, [copied, resetAfterMs]);

  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
    } catch {
      // No clipboard permission: the value is still on screen to select by hand.
    }
  };

  return { copied, copy };
}
