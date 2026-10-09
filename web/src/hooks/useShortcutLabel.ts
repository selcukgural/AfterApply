"use client";

import { useSyncExternalStore } from "react";
import { prefersCommandKey } from "@/lib/quickFind/quickFind";

const subscribe = () => () => {};

/** "⌘K" on a Mac, "Ctrl K" elsewhere — "Ctrl K" on the server and in the first paint, so the
 *  hydrated markup matches what the server sent. */
export function useQuickFindShortcutLabel(): string {
  const mac = useSyncExternalStore(
    subscribe,
    () => prefersCommandKey(navigator.platform || navigator.userAgent),
    () => false,
  );
  return mac ? "⌘K" : "Ctrl K";
}
