import { describe, expect, it } from "vitest";
import { salaryMarketPath, salaryMarketRedirectForPath, salaryOccupationPath, salaryOccupationPaths } from "./path";

describe("salary pages' addresses", () => {
  it("are translated per language, the occupation slug is not", () => {
    expect(salaryMarketPath("tr")).toBe("/maaslar");
    expect(salaryMarketPath("en")).toBe("/salaries");
    expect(salaryOccupationPath("en", "back-end-developer")).toBe("/salaries/back-end-developer");
    expect(salaryOccupationPaths("cto")).toEqual({ tr: "/maaslar/cto", en: "/salaries/cto" });
  });

  it("send the other language's spelling to the right one", () => {
    expect(salaryMarketRedirectForPath("/en/maaslar")).toBe("/en/salaries");
    expect(salaryMarketRedirectForPath("/tr/salaries/cto")).toBe("/tr/maaslar/cto");
    expect(salaryMarketRedirectForPath("/en/maaslar/back-end-developer/")).toBe("/en/salaries/back-end-developer");
  });

  it("leave the right spelling and everything else alone", () => {
    expect(salaryMarketRedirectForPath("/tr/maaslar")).toBeNull();
    expect(salaryMarketRedirectForPath("/en/salaries/cto")).toBeNull();
    expect(salaryMarketRedirectForPath("/tr/maaslarim")).toBeNull();
    expect(salaryMarketRedirectForPath("/tr/maaslar/a/b")).toBeNull();
  });
});
