import { describe, expect, it } from "vitest";
import { shouldAutoLinkBlogText } from "./autolink";

describe("shouldAutoLinkBlogText", () => {
  it("leaves domain-shaped brand names in running text alone", () => {
    for (const text of ["Kariyer.net", "kariyer.net", "Kariyer.net'in", "LinkedIn.com", "e-kariyerim.com", "Yenibiris.com"]) {
      expect(shouldAutoLinkBlogText(text)).toBe(false);
    }
  });

  it("links what is unmistakably an address", () => {
    expect(shouldAutoLinkBlogText("https://www.kariyer.net/tum-basvurular")).toBe(true);
    expect(shouldAutoLinkBlogText("http://example.com")).toBe(true);
    expect(shouldAutoLinkBlogText("www.kariyer.net")).toBe(true);
  });

  it("does not link a protocol or a www. with nothing after it", () => {
    expect(shouldAutoLinkBlogText("https://")).toBe(false);
    expect(shouldAutoLinkBlogText("www.")).toBe(false);
    expect(shouldAutoLinkBlogText("mailto:someone@example.com")).toBe(false);
  });
});
