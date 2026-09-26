import { describe, expect, it } from "vitest";
import { ADMIN_CONTRIBUTION_TABS, isAdminTabActive } from "./adminTabs";

describe("isAdminTabActive", () => {
  it("matches a tab on its own page", () => {
    expect(isAdminTabActive("/admin/metrics", "/admin/metrics")).toBe(true);
    expect(isAdminTabActive("/admin/reviews", "/admin/reviews")).toBe(true);
    expect(isAdminTabActive("/admin/reviews/reports", "/admin/reviews/reports")).toBe(true);
  });

  it("keeps the Reviews tab lit on its two sibling tables", () => {
    expect(isAdminTabActive("/admin/reviews/salaries", "/admin/reviews")).toBe(true);
    expect(isAdminTabActive("/admin/reviews/experiences", "/admin/reviews")).toBe(true);
    expect(isAdminTabActive("/admin/blog", "/admin/blog")).toBe(true);
    expect(isAdminTabActive("/admin/blog/0199a0a0-0000-7000-8000-000000000001", "/admin/blog")).toBe(true);
    expect(isAdminTabActive("/admin/blog", "/admin/reviews")).toBe(false);
    expect(isAdminTabActive("/admin/comments", "/admin/comments")).toBe(true);
    expect(isAdminTabActive("/admin/comments/0199a0a0-0000-7000-8000-000000000001", "/admin/comments")).toBe(true);
    expect(isAdminTabActive("/admin/comments", "/admin/blog")).toBe(false);
  });

  it("keeps the Guide tab lit inside its editor, and apart from the Blog tab (2026-09-26)", () => {
    expect(isAdminTabActive("/admin/guide", "/admin/guide")).toBe(true);
    expect(isAdminTabActive("/admin/guide/new", "/admin/guide")).toBe(true);
    expect(isAdminTabActive("/admin/guide/0199a0a0-0000-7000-8000-000000000001", "/admin/guide")).toBe(true);
    expect(isAdminTabActive("/admin/guide/new", "/admin/blog")).toBe(false);
    expect(isAdminTabActive("/admin/blog/new", "/admin/guide")).toBe(false);
  });

  it("does not light Reviews on Reports, which is its own tab, nor anything else", () => {
    expect(isAdminTabActive("/admin/reviews/reports", "/admin/reviews")).toBe(false);
    expect(isAdminTabActive("/admin/reviews", "/admin/reviews/reports")).toBe(false);
    expect(isAdminTabActive("/admin/reviews/salaries", "/admin/metrics")).toBe(false);
    expect(isAdminTabActive("/admin/payments", "/admin/reviews")).toBe(false);
  });

  it("lists the three contribution tables under /admin/reviews", () => {
    expect(ADMIN_CONTRIBUTION_TABS.map((tab) => tab.href)).toEqual(["/admin/reviews", "/admin/reviews/salaries", "/admin/reviews/experiences"]);
    for (const tab of ADMIN_CONTRIBUTION_TABS) {
      expect(isAdminTabActive(tab.href, "/admin/reviews")).toBe(true);
    }
  });
});
