"use client";

import { useEffect, useState } from "react";
import type { CoverIconNode } from "@/lib/blog/coverCard";
import type { CoverIconIndex } from "@/lib/blog/coverIconSearch";

/** The icon set in the browser: every icon's drawing, plus the names and tags the search reads. */
export interface CoverIconSet extends CoverIconIndex {
  nodes: Record<string, CoverIconNode>;
}

let pending: Promise<CoverIconSet> | null = null;

/**
 * Loads the set once per page, as its own chunks — it is only the blog editor's, and only a blog
 * post's (a guide has no generated cover), so no other page pays for it.
 */
export function loadCoverIconSet(): Promise<CoverIconSet> {
  pending ??= Promise.all([import("lucide-static/icon-nodes.json"), import("lucide-static/tags.json")])
    .then(([nodesModule, tagsModule]) => {
      const nodes = nodesModule.default as unknown as Record<string, CoverIconNode>;
      const tags = tagsModule.default as unknown as Record<string, string[]>;
      return { nodes, tags, names: Object.keys(nodes).sort() };
    })
    .catch((err: unknown) => {
      // A failed chunk load is retried by the next caller rather than remembered.
      pending = null;
      throw err;
    });
  return pending;
}

/** The set once it has loaded; null while it loads, "error" when it could not (the preview then
 *  draws without an icon and the picker says it could not load). */
export function useCoverIconSet(enabled = true): CoverIconSet | null | "error" {
  const [set, setSet] = useState<CoverIconSet | null | "error">(null);
  useEffect(() => {
    if (!enabled) return;
    let live = true;
    loadCoverIconSet().then(
      (loaded) => {
        if (live) setSet(loaded);
      },
      () => {
        if (live) setSet("error");
      },
    );
    return () => {
      live = false;
    };
  }, [enabled]);
  return set;
}
