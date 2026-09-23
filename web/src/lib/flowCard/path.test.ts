import { describe, expect, it } from "vitest";
import { flowCardOf, flowCardPath, flowCardRedirectForPath } from "./path";

describe("the shared flow card's address", () => {
  it("is /akis in Turkish and /flow in English", () => {
    expect(flowCardPath("tr", "abc")).toBe("/akis/abc");
    expect(flowCardPath("en", "abc")).toBe("/flow/abc");
  });

  it("reads the card out of either spelling", () => {
    expect(flowCardOf("/en/flow/1-2")).toEqual({ locale: "en", card: "1-2" });
    expect(flowCardOf("/tr/akis/1-2/")).toEqual({ locale: "tr", card: "1-2" });
    expect(flowCardOf("/tr/akis")).toBeNull();
    expect(flowCardOf("/tr/dashboard")).toBeNull();
  });

  it("redirects only the wrong-locale spelling", () => {
    expect(flowCardRedirectForPath("/tr/flow/1-2")).toBe("/tr/akis/1-2");
    expect(flowCardRedirectForPath("/en/akis/1-2")).toBe("/en/flow/1-2");
    expect(flowCardRedirectForPath("/tr/akis/1-2")).toBeNull();
    expect(flowCardRedirectForPath("/en/flow/1-2")).toBeNull();
  });
});
