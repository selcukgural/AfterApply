import { describe, expect, it } from "vitest";
import type { ApplicationSummaryResponse } from "@/types/api";
import { isQuickFindShortcut, moveActive, prefersCommandKey, otherApplicationsAtCompany, quickCardTarget } from "./quickFind";

function row(id: string, companyId: string, appliedAt: string): ApplicationSummaryResponse {
  return { id, companyId, companyName: companyId, jobTitle: "Engineer", status: "Applied", appliedAt, updatedAt: appliedAt };
}

describe("quickCardTarget", () => {
  it("opens the latest application when every row found is at one company", () => {
    const items = [row("old", "lodos", "2026-03-01T09:00:00+00:00"), row("new", "lodos", "2026-09-27T09:00:00Z")];
    expect(quickCardTarget("lodos", items)).toBe("new");
  });

  it("stays a plain list when the search spans companies", () => {
    const items = [row("a", "lodos", "2026-09-27T09:00:00Z"), row("b", "kuzey", "2026-09-28T09:00:00Z")];
    expect(quickCardTarget("developer", items)).toBeNull();
  });

  it("needs an active search and at least one row", () => {
    expect(quickCardTarget("", [row("a", "lodos", "2026-09-27T09:00:00Z")])).toBeNull();
    expect(quickCardTarget("   ", [row("a", "lodos", "2026-09-27T09:00:00Z")])).toBeNull();
    expect(quickCardTarget("lodos", [])).toBeNull();
  });
});

describe("otherApplicationsAtCompany", () => {
  it("leaves out the card's own application and keeps at most three", () => {
    const items = ["x", "a", "b", "c", "d"].map((id) => row(id, "lodos", "2026-09-27T09:00:00Z"));
    expect(otherApplicationsAtCompany(items, "x").map((item) => item.id)).toEqual(["a", "b", "c"]);
  });
});

describe("moveActive", () => {
  it("wraps round in both directions", () => {
    expect(moveActive(2, 3, "ArrowDown")).toBe(0);
    expect(moveActive(0, 3, "ArrowUp")).toBe(2);
    expect(moveActive(0, 0, "ArrowDown")).toBe(0);
  });
});

describe("isQuickFindShortcut", () => {
  const key = (k: string, mods: Partial<Record<"ctrlKey" | "metaKey" | "altKey" | "shiftKey", boolean>> = {}) =>
    ({ key: k, ctrlKey: false, metaKey: false, altKey: false, shiftKey: false, ...mods });

  it("answers Ctrl+K and ⌘K", () => {
    expect(isQuickFindShortcut(key("k", { ctrlKey: true }))).toBe(true);
    expect(isQuickFindShortcut(key("K", { metaKey: true }))).toBe(true);
  });

  it("leaves plain typing and other chords alone", () => {
    expect(isQuickFindShortcut(key("k"))).toBe(false);
    expect(isQuickFindShortcut(key("k", { ctrlKey: true, shiftKey: true }))).toBe(false);
    expect(isQuickFindShortcut(key("j", { ctrlKey: true }))).toBe(false);
  });
});

describe("prefersCommandKey", () => {
  it("names ⌘ on Apple platforms only", () => {
    expect(prefersCommandKey("MacIntel")).toBe(true);
    expect(prefersCommandKey("Win32")).toBe(false);
    expect(prefersCommandKey("Linux x86_64")).toBe(false);
  });
});
