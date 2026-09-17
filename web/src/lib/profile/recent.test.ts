import { describe, expect, it } from "vitest";
import { RECENT_CONTRIBUTIONS, takeRecent } from "./recent";

const at = (id: string, submittedAt: string) => ({ id, submittedAt });

describe("takeRecent", () => {
  it("returns the newest first and stops at the limit", () => {
    const items = [at("a", "2026-09-01T00:00:00Z"), at("b", "2026-09-12T00:00:00Z"), at("c", "2026-09-03T00:00:00Z"), at("d", "2026-08-20T00:00:00Z")];

    expect(takeRecent(items).map((i) => i.id)).toEqual(["b", "c", "a"]);
    expect(RECENT_CONTRIBUTIONS).toBe(3);
  });

  it("leaves the input untouched and copes with fewer items than the limit", () => {
    const items = [at("a", "2026-09-01T00:00:00Z"), at("b", "2026-09-12T00:00:00Z")];

    expect(takeRecent(items, 3).map((i) => i.id)).toEqual(["b", "a"]);
    expect(items.map((i) => i.id)).toEqual(["a", "b"]);
    expect(takeRecent(items, 0)).toEqual([]);
  });
});
