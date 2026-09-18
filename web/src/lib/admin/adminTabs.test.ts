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
