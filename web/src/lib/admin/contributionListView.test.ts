import { describe, expect, it } from "vitest";
import { buildContributionQueryString, parseContributionFilters, toContributionSearchParams, withCompanyChange } from "./contributionListView";

describe("contribution list filters", () => {
  it("reads the company and the page from the URL, defaulting to page 1", () => {
    expect(parseContributionFilters(new URLSearchParams(""))).toEqual({ company: "", page: 1 });
    expect(parseContributionFilters(new URLSearchParams("company=Acme&page=3"))).toEqual({ company: "Acme", page: 3 });
    expect(parseContributionFilters(new URLSearchParams("page=0"))).toEqual({ company: "", page: 1 });
    expect(parseContributionFilters(new URLSearchParams("page=abc"))).toEqual({ company: "", page: 1 });
  });

  it("caps a pasted company name at what the API accepts", () => {
    expect(parseContributionFilters(new URLSearchParams(`company=${"x".repeat(150)}`)).company).toHaveLength(100);
  });

  it("resets the page when the company changes", () => {
    expect(withCompanyChange({ company: "", page: 4 }, "Acme")).toEqual({ company: "Acme", page: 1 });
  });

  it("serialises only what is set, trimmed", () => {
    expect(buildContributionQueryString({ company: "", page: 1 })).toBe("");
    expect(buildContributionQueryString({ company: "  Acme ", page: 1 })).toBe("?company=Acme");
    expect(buildContributionQueryString({ company: "", page: 2 })).toBe("?page=2");
    expect(toContributionSearchParams({ company: "Acme", page: 2 }).toString()).toBe("company=Acme&page=2");
  });
});
