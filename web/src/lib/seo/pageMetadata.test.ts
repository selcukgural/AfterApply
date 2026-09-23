import { describe, expect, it } from "vitest";
import { buildMetadata } from "./pageMetadata";

const base = { locale: "tr", path: "/x", title: "T", description: "D" };

describe("buildMetadata robots", () => {
  it("says nothing about robots for an indexable page", () => {
    expect(buildMetadata(base).robots).toBeUndefined();
  });

  it("keeps a non-indexed page's links unfollowed unless asked", () => {
    expect(buildMetadata({ ...base, index: false }).robots).toEqual({ index: false, follow: false });
  });

  it("lets a non-indexed page keep its links followable", () => {
    expect(buildMetadata({ ...base, index: false, follow: true }).robots).toEqual({ index: false, follow: true });
  });
});
