import { afterEach, describe, expect, it, vi } from "vitest";
import { MODERATION_GUIDE_DISMISSED_KEY, isModerationGuideDismissed, moderationGuideStore } from "./moderationGuideStore";

describe("moderation guide dismissal flag", () => {
  it("is not dismissed on a first visit", () => {
    expect(isModerationGuideDismissed(null)).toBe(false);
    expect(isModerationGuideDismissed(undefined)).toBe(false);
  });

  it("is dismissed only by the exact flag the store writes", () => {
    expect(isModerationGuideDismissed("1")).toBe(true);
    expect(isModerationGuideDismissed("true")).toBe(false);
    expect(isModerationGuideDismissed("")).toBe(false);
  });

  it("uses a key inside the published aa_ namespace", () => {
    expect(MODERATION_GUIDE_DISMISSED_KEY).toMatch(/^aa_[a-z_]+$/);
  });
});

describe("moderation guide store", () => {
  // The suite runs in node: there is no window and no localStorage. A fake one is put on
  // globalThis for the tests that need it and removed afterwards.
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function stubStorage(initial: Record<string, string> = {}) {
    const map = new Map(Object.entries(initial));
    const storage = {
      getItem: (key: string) => map.get(key) ?? null,
      setItem: (key: string, value: string) => void map.set(key, value),
      removeItem: (key: string) => void map.delete(key),
    };
    vi.stubGlobal("window", { localStorage: storage });
    vi.stubGlobal("localStorage", storage);
    return map;
  }

  it("renders closed on the server, where there is nothing to read", () => {
    expect(moderationGuideStore.isOpenOnServer()).toBe(false);
  });

  it("is open on a first visit and stays closed once dismissed, across a reload", () => {
    const map = stubStorage();
    expect(moderationGuideStore.isOpen()).toBe(true);

    moderationGuideStore.setOpen(false);
    expect(map.get(MODERATION_GUIDE_DISMISSED_KEY)).toBe("1");
    expect(moderationGuideStore.isOpen()).toBe(false);

    // Reopening forgets the dismissal rather than storing a second flag.
    moderationGuideStore.setOpen(true);
    expect(map.has(MODERATION_GUIDE_DISMISSED_KEY)).toBe(false);
    expect(moderationGuideStore.isOpen()).toBe(true);
  });

  it("tells subscribers about every change and lets them leave", () => {
    stubStorage();
    const listener = vi.fn();
    const unsubscribe = moderationGuideStore.subscribe(listener);

    moderationGuideStore.setOpen(false);
    expect(listener).toHaveBeenCalledTimes(1);

    unsubscribe();
    moderationGuideStore.setOpen(true);
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("opens every time when storage is unavailable or throws", () => {
    expect(moderationGuideStore.isOpen()).toBe(true);

    const broken = {
      getItem: () => {
        throw new Error("blocked");
      },
      setItem: () => {
        throw new Error("blocked");
      },
      removeItem: () => {
        throw new Error("blocked");
      },
    };
    vi.stubGlobal("window", { localStorage: broken });
    vi.stubGlobal("localStorage", broken);
    expect(moderationGuideStore.isOpen()).toBe(true);
    expect(() => moderationGuideStore.setOpen(false)).not.toThrow();
  });
});
