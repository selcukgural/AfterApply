import { describe, expect, it } from "vitest";
import { RELEASE_NOTES, compareVersions, noticeState } from "../whats-new.js";

const VERSION = "0.9.2";

describe("compareVersions", () => {
  it.each([
    ["0.9.3", "0.9.2", 1],
    ["0.10.0", "0.9.2", 1],
    ["1.0", "0.9.9", 1],
    ["0.9.2", "0.9.2", 0],
    ["0.9.2", "0.9.2.0", 0],
    ["0.9.1", "0.9.2", -1],
  ])("%s against %s", (a, b, sign) => {
    expect(Math.sign(compareVersions(a, b))).toBe(sign);
  });
});

describe("noticeState", () => {
  it("opens the notes and clears the dot right after an update", () => {
    const state = noticeState({ whatsNew: { version: VERSION, dismissed: false, badge: true } }, VERSION, "tr");

    expect(state.bannerOpen).toBe(true);
    expect(state.clearBadge).toBe(true);
    expect(state.notes).toEqual(RELEASE_NOTES[VERSION].tr);
  });

  it("keeps them closed once closed, but still offers them from the footer", () => {
    const state = noticeState({ whatsNew: { version: VERSION, dismissed: true, badge: false } }, VERSION, "en");

    expect(state.bannerOpen).toBe(false);
    expect(state.clearBadge).toBe(false);
    expect(state.notes).toEqual(RELEASE_NOTES[VERSION].en);
  });

  // A new user is not greeted with a changelog: nothing is recorded on a fresh install.
  it("shows nothing unasked after a fresh install", () => {
    expect(noticeState({ whatsNew: null }, VERSION, "en").bannerOpen).toBe(false);
  });

  it("ignores a record left over from an older version", () => {
    const state = noticeState({ whatsNew: { version: "0.9.1", dismissed: false, badge: true } }, VERSION, "en");

    expect(state.bannerOpen).toBe(false);
    expect(state.clearBadge).toBe(false);
  });

  it("has nothing to show for a version without notes", () => {
    const state = noticeState({ whatsNew: { version: "0.0.1", dismissed: false, badge: true } }, "0.0.1", "en");

    expect(state.notes).toBeNull();
    expect(state.bannerOpen).toBe(false);
  });

  it("falls back to English for a language without notes", () => {
    expect(noticeState({}, VERSION, "de").notes).toEqual(RELEASE_NOTES[VERSION].en);
  });

  it("offers a restart only into a newer build", () => {
    expect(noticeState({ pendingUpdate: "0.9.3" }, VERSION, "en").pendingVersion).toBe("0.9.3");
    // The update already applied: the stored version is the one running, or older.
    expect(noticeState({ pendingUpdate: VERSION }, VERSION, "en").pendingVersion).toBeNull();
    expect(noticeState({ pendingUpdate: "0.9.1" }, VERSION, "en").pendingVersion).toBeNull();
  });
});
