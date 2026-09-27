import { describe, expect, it } from "vitest";
import { looksLikeJobLink } from "./pastedLink";

describe("looksLikeJobLink", () => {
  it("accepts posting links, with or without a scheme", () => {
    expect(looksLikeJobLink("https://www.linkedin.com/jobs/view/4012345678/?refId=x")).toBe(true);
    expect(looksLikeJobLink("  http://kariyer.net/is-ilani/acme-123  ")).toBe(true);
    expect(looksLikeJobLink("kariyer.net/is-ilani/acme-123")).toBe(true);
  });

  it("leaves ordinary search text alone", () => {
    expect(looksLikeJobLink("backend developer")).toBe(false);
    expect(looksLikeJobLink("Acme")).toBe(false);
    expect(looksLikeJobLink("C#/.NET")).toBe(false);
    expect(looksLikeJobLink("node.js")).toBe(false);
    expect(looksLikeJobLink("javascript:alert(1)")).toBe(false);
    expect(looksLikeJobLink("")).toBe(false);
  });
});
