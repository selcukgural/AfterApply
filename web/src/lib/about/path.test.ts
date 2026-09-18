import { describe, expect, it } from "vitest";
import { aboutPath, aboutRedirectForPath } from "./path";

describe("aboutPath", () => {
  it("is /hakkimizda under tr and /about under en, defaulting to Turkish", () => {
    expect(aboutPath("tr")).toBe("/hakkimizda");
    expect(aboutPath("en")).toBe("/about");
    expect(aboutPath("de")).toBe("/hakkimizda");
  });
});

describe("aboutRedirectForPath", () => {
  it("sends the wrong slug to the right one, in both directions", () => {
    expect(aboutRedirectForPath("/en/hakkimizda")).toBe("/en/about");
    expect(aboutRedirectForPath("/tr/about/")).toBe("/tr/hakkimizda");
  });

  it("leaves the right addresses and everything else alone", () => {
    expect(aboutRedirectForPath("/tr/hakkimizda")).toBeNull();
    expect(aboutRedirectForPath("/en/about")).toBeNull();
    expect(aboutRedirectForPath("/tr/help/about")).toBeNull();
    expect(aboutRedirectForPath("/about")).toBeNull();
  });
});
