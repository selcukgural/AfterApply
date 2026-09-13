/**
 * Whether the moderation guide panel on /admin/reviews is open.
 *
 * The panel opens by itself the first time a moderator lands on the queue and stays out of the way
 * once they close it — that "once" has to survive a reload, so it lives in localStorage rather
 * than component state. The key is in the cookie policy's browser-storage inventory
 * (`browserStorage.test.ts` pins that, by spotting the literal `localStorage.setItem` below); it
 * is a bare preference flag, written only on an admin page, and carries nothing about the user.
 *
 * Shaped as an external store (subscribe/getSnapshot) so the page reads it through
 * `useSyncExternalStore` — the same pattern as `authStore` — instead of mirroring localStorage
 * into state from an effect.
 */
export const MODERATION_GUIDE_DISMISSED_KEY = "aa_moderation_guide_dismissed";

/** The one value that means "dismissed". Anything else — a hand-edited or stale entry — reads as
 *  not dismissed, so the guide shows one extra time, which is the harmless direction to fail in. */
export function isModerationGuideDismissed(stored: string | null | undefined): boolean {
  return stored === "1";
}

const listeners = new Set<() => void>();

function readStored(): string | null {
  try {
    return typeof window === "undefined" ? null : window.localStorage.getItem(MODERATION_GUIDE_DISMISSED_KEY);
  } catch {
    // Storage access can throw (privacy mode, blocked site data); the guide then simply opens
    // every time.
    return null;
  }
}

export const moderationGuideStore = {
  subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
  /** Open unless dismissed — the first visit sees the guide. */
  isOpen(): boolean {
    return !isModerationGuideDismissed(readStored());
  },
  /** The server has no storage to read; rendering closed there avoids a flash of the whole panel. */
  isOpenOnServer(): boolean {
    return false;
  },
  setOpen(open: boolean): void {
    try {
      if (open) localStorage.removeItem(MODERATION_GUIDE_DISMISSED_KEY);
      else localStorage.setItem(MODERATION_GUIDE_DISMISSED_KEY, "1");
    } catch {
      // A preference that cannot be written is not worth an error; the panel still toggles for
      // this page load.
    }
    for (const listener of listeners) listener();
  },
};
