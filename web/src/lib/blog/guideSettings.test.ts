import { describe, expect, it } from "vitest";
import { MAX_RELATED_GUIDES, setRelatedAt } from "./guideSettings";

describe("setRelatedAt", () => {
  it("fills the slots in order", () => {
    expect(setRelatedAt([], 0, "a")).toEqual(["a"]);
    expect(setRelatedAt(["a"], 1, "b")).toEqual(["a", "b"]);
  });

  it("replaces a slot in place", () => {
    expect(setRelatedAt(["a", "b"], 0, "c")).toEqual(["c", "b"]);
  });

  it("pulls the later slots up when one is emptied, so the list has no hole", () => {
    expect(setRelatedAt(["a", "b"], 0, null)).toEqual(["b"]);
    expect(setRelatedAt(["a", "b"], 1, null)).toEqual(["a"]);
  });

  it("never sends a guide twice — picking one from the other slot swaps the two", () => {
    expect(setRelatedAt(["a", "b"], 1, "a")).toEqual(["b", "a"]);
    expect(setRelatedAt(["a", "b"], 0, "b")).toEqual(["b", "a"]);
    expect(setRelatedAt(["a"], 1, "a")).toEqual(["a"]);
  });

  it("keeps to the cap the API accepts", () => {
    expect(setRelatedAt(["a", "b"], 2, "c").length).toBeLessThanOrEqual(MAX_RELATED_GUIDES);
  });
});
