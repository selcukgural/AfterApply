import { describe, expect, it } from "vitest";
import {
  buildModerationQueryString,
  parseModerationFilters,
  toSearchParams,
  withFilterChange,
} from "./moderationListView";

describe("parseModerationFilters", () => {
  it("validates every parameter rather than casting it", () => {
    const filters = parseModerationFilters(
      new URLSearchParams("status=Bogus&company=Acme&from=2026-09-01&to=not-a-date&page=x"),
    );

    expect(filters).toEqual({ status: "", company: "Acme", from: "2026-09-01", to: "", page: 1 });
  });

  it("keeps a valid status and page", () => {
    expect(parseModerationFilters(new URLSearchParams("status=Rejected&page=3"))).toMatchObject({
      status: "Rejected",
      page: 3,
    });
  });
});

describe("withFilterChange", () => {
  it("resets the page on any filter change", () => {
    const current = { status: "" as const, company: "", from: "", to: "", page: 4 };

    expect(withFilterChange(current, { company: "acme" }).page).toBe(1);
  });
});

describe("buildModerationQueryString", () => {
  it("widens a date-only 'to' to the end of that day and omits empty filters", () => {
    const query = buildModerationQueryString({ status: "Pending", company: " Acme ", from: "2026-09-01", to: "2026-09-12", page: 2 });

    expect(query).toBe("?status=Pending&company=Acme&from=2026-09-01T00%3A00%3A00Z&to=2026-09-12T23%3A59%3A59Z&page=2");
    expect(buildModerationQueryString({ status: "", company: "", from: "", to: "", page: 1 })).toBe("");
  });

  it("round-trips through the page's own search params", () => {
    const filters = { status: "Approved" as const, company: "acme", from: "2026-09-01", to: "", page: 2 };

    expect(parseModerationFilters(toSearchParams(filters))).toEqual(filters);
  });
});
