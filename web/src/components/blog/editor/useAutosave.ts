"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { AdminBlogPost, SaveBlogDraftRequest } from "@/types/api";
import { adminBlogApi } from "@/lib/api/blog";
import { ApiError } from "@/lib/api/httpClient";
import { hasDraftText } from "@/lib/blog/draftText";
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
  /** Null for a post that does not exist yet: the first save creates it (see `onCreated`). */
  postId: string | null;
  initialRevision: number;
  /** Everything the draft save carries, read at save time (never stale). */
  buildRequest: () => Omit<SaveBlogDraftRequest, "revision">;
  /** The post the first save created — the caller takes its id and the URL from here. */
  onCreated: (post: AdminBlogPost) => void;
}

export interface Autosave {
  state: AutosaveState;
  /** Call on every edit; the scheduler decides when the save goes out. */
  markEdited: () => void;
  /** Saves now if anything is unsaved, and waits for it — what publish calls first. Resolves
   *  false when the save failed, the draft is in conflict, or there is no post yet and nothing
   *  written to create one from. */
  flush: () => Promise<boolean>;
  /** The post's id, creating the post first when it does not exist yet. Null when there is
   *  nothing written to create it from — what an image upload needs before it can happen. */
  ensurePost: () => Promise<string | null>;
}

/**
 * The timer half of the autosave: the pure scheduler in `lib/blog/autosaveScheduler.ts` decides,
 * this hook ticks once a second, sends the request and reports back. One request in flight at a
 * time; a 409 stops the machine (the indicator says "reload"). Also warns on `beforeunload`
 * while anything is unsaved.
 *
 * A new post has no id until its first save: that save is a create (POST) instead of a draft
 * PUT, and it only goes out once something has been written (`hasDraftText`) — until then the
 * tick finds nothing to send and the machine returns to clean, so a "new post" that is opened
 * and abandoned leaves no row (2026-09-19).
 */
export function useAutosave({ postId, initialRevision, buildRequest, onCreated }: UseAutosaveOptions): Autosave {
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
  const onCreatedRef = useRef(onCreated);
  useEffect(() => {
    buildRef.current = buildRequest;
    onCreatedRef.current = onCreated;
  });
  // The id the next save goes to. Set from the prop, and by the create itself the moment it
  // returns, so a tick that fires before the parent has re-rendered does not create twice.
  const postIdRef = useRef(postId);
  useEffect(() => {
    postIdRef.current = postId;
  }, [postId]);
  const inFlight = useRef<Promise<boolean> | null>(null);

  const save = useCallback((): Promise<boolean> => {
    if (inFlight.current) return inFlight.current;
    const fields = buildRef.current();
    const id = postIdRef.current;
    if (id === null && !hasDraftText(fields)) {
      dispatch({ type: "saveSkipped" });
      return Promise.resolve(false);
    }
    const revision = stateRef.current.revision;
    dispatch({ type: "saveStarted" });
    const send =
      id === null
        ? adminBlogApi
            .create({
              title: fields.title,
              excerpt: fields.excerpt,
              contentJson: fields.contentJson,
              contentHtml: fields.contentHtml,
              language: fields.language,
              slug: fields.slug,
              translationOfPostId: fields.translationOfPostId,
            })
            .then((post) => {
              postIdRef.current = post.id;
              onCreatedRef.current(post);
              return { revision: post.revision };
            })
        : adminBlogApi.saveDraft(id, { ...fields, revision });
    const request = send
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
  }, [dispatch]);

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
    // No post yet and nothing to make one from: not "saved", whatever the machine says.
    if (postIdRef.current === null && !hasDraftText(buildRef.current())) return false;
    if (!hasUnsavedChanges(current)) return true;
    return save();
  }, [save]);

  const ensurePost = useCallback(async () => {
    if (postIdRef.current !== null) return postIdRef.current;
    await flush();
    return postIdRef.current;
  }, [flush]);

  return { state, markEdited, flush, ensurePost };
}
