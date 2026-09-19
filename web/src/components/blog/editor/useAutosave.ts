"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { SaveBlogDraftRequest } from "@/types/api";
import { adminBlogApi } from "@/lib/api/blog";
import { ApiError } from "@/lib/api/httpClient";
import {
  autosaveReducer,
  hasUnsavedChanges,
  initialAutosaveState,
  shouldAutosave,
  type AutosaveAction,
  type AutosaveState,
} from "@/lib/blog/autosaveScheduler";

const TICK_MS = 1_000;

interface UseAutosaveOptions {
  postId: string;
  initialRevision: number;
  /** Everything the draft save carries, read at save time (never stale). */
  buildRequest: () => Omit<SaveBlogDraftRequest, "revision">;
}

export interface Autosave {
  state: AutosaveState;
  /** Call on every edit; the scheduler decides when the save goes out. */
  markEdited: () => void;
  /** Saves now if anything is unsaved, and waits for it — what publish calls first. Resolves
   *  false when the save failed or the draft is in conflict. */
  flush: () => Promise<boolean>;
}

/**
 * The timer half of the autosave: the pure scheduler in `lib/blog/autosaveScheduler.ts` decides,
 * this hook ticks once a second, sends the request and reports back. One request in flight at a
 * time; a 409 stops the machine (the indicator says "reload"). Also warns on `beforeunload`
 * while anything is unsaved.
 */
export function useAutosave({ postId, initialRevision, buildRequest }: UseAutosaveOptions): Autosave {
  const [state, setState] = useState(() => initialAutosaveState(initialRevision));
  // The reducer is applied eagerly onto a ref, so a continuation that runs right after an awaited
  // save (flush) reads the post-save state without waiting for React to re-render.
  const stateRef = useRef(state);
  const dispatch = useCallback((action: AutosaveAction) => {
    stateRef.current = autosaveReducer(stateRef.current, action);
    setState(stateRef.current);
  }, []);
  // Read at save time, so the request carries what is on screen then, not what was there when
  // the hook last rendered.
  const buildRef = useRef(buildRequest);
  useEffect(() => {
    buildRef.current = buildRequest;
  });
  const inFlight = useRef<Promise<boolean> | null>(null);

  const save = useCallback((): Promise<boolean> => {
    if (inFlight.current) return inFlight.current;
    const revision = stateRef.current.revision;
    dispatch({ type: "saveStarted" });
    const request = adminBlogApi
      .saveDraft(postId, { ...buildRef.current(), revision })
      .then((saved) => {
        dispatch({ type: "saveSucceeded", revision: saved.revision, at: Date.now() });
        return true;
      })
      .catch((err: unknown) => {
        if (err instanceof ApiError && err.status === 409) {
          dispatch({ type: "saveConflicted" });
        } else {
          dispatch({ type: "saveFailed", message: err instanceof Error ? err.message : String(err), at: Date.now() });
        }
        return false;
      })
      .finally(() => {
        inFlight.current = null;
      });
    inFlight.current = request;
    return request;
  }, [postId, dispatch]);

  useEffect(() => {
    const timer = window.setInterval(() => {
      if (shouldAutosave(stateRef.current, Date.now())) void save();
    }, TICK_MS);
    return () => window.clearInterval(timer);
  }, [save]);

  useEffect(() => {
    const warn = (event: BeforeUnloadEvent) => {
      if (hasUnsavedChanges(stateRef.current)) event.preventDefault();
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, []);

  const markEdited = useCallback(() => dispatch({ type: "edited", at: Date.now() }), [dispatch]);

  const flush = useCallback(async () => {
    if (inFlight.current) await inFlight.current;
    const current = stateRef.current;
    if (current.status === "conflict") return false;
    if (!hasUnsavedChanges(current)) return true;
    return save();
  }, [save]);

  return { state, markEdited, flush };
}
