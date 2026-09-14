"use client";

import { useSyncExternalStore } from "react";
import { readDocumentTheme, type Theme } from "@/lib/theme/theme";

/**
 * The theme `<html>` is currently showing, as React state. Subscribes to the class attribute the
 * boot script and `applyTheme` write, so a switch anywhere re-renders every reader; the server
 * snapshot is "light", which is what the server also renders, so hydration matches.
 */
export function useDocumentTheme(): Theme {
  return useSyncExternalStore(subscribe, readDocumentTheme, () => "light");
}

function subscribe(onChange: () => void): () => void {
  const observer = new MutationObserver(onChange);
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ["class"] });
  return () => observer.disconnect();
}
