import { describe, expect, it } from "vitest";
import { isLinkedInProfileUrl, safeMailtoUrl } from "@/lib/url/externalLink";

describe("isLinkedInProfileUrl", () => {
  it("accepts a profile URL on linkedin.com and its locale subdomains", () => {
    expect(isLinkedInProfileUrl("https://www.linkedin.com/in/zeynep-a")).toBe(true);
    expect(isLinkedInProfileUrl("https://tr.linkedin.com/in/zeynep-a")).toBe(true);
    expect(isLinkedInProfileUrl("https://linkedin.com/in/zeynep-a/")).toBe(true);
  });

  it("rejects a company page, which is a different field on the same form", () => {
    expect(isLinkedInProfileUrl("https://www.linkedin.com/company/oplog/")).toBe(false);
  });

  it("rejects another host, including one that merely ends in the right letters", () => {
    expect(isLinkedInProfileUrl("https://evil.example/in/zeynep-a")).toBe(false);
    expect(isLinkedInProfileUrl("https://notlinkedin.com/in/zeynep-a")).toBe(false);
  });

  it("rejects non-https and unparsable values", () => {
    expect(isLinkedInProfileUrl("http://www.linkedin.com/in/zeynep-a")).toBe(false);
    expect(isLinkedInProfileUrl("javascript:alert(1)")).toBe(false);
    expect(isLinkedInProfileUrl("linkedin.com/in/zeynep-a")).toBe(false);
  });
});

describe("safeMailtoUrl", () => {
  it("builds a mailto with the address left literal", () => {
    // Percent-encoding would turn the "@" into %40, which strict mail clients may reject.
    expect(safeMailtoUrl("talent@oplog.com")).toBe("mailto:talent@oplog.com");
    expect(safeMailtoUrl("  talent@oplog.com  ")).toBe("mailto:talent@oplog.com");
  });

  it("refuses anything that would add recipients or mailto headers", () => {
    // The field is user-entered and lands in an href — a smuggled bcc or body must not be
    // possible, so these are rejected outright rather than escaped.
    expect(safeMailtoUrl("a@b.com,c@d.com")).toBeNull();
    expect(safeMailtoUrl("a@b.com?bcc=c@d.com")).toBeNull();
    expect(safeMailtoUrl("a@b.com&subject=x")).toBeNull();
    expect(safeMailtoUrl("a@b.com\nc@d.com")).toBeNull();
    expect(safeMailtoUrl("Zeynep <a@b.com>")).toBeNull();
  });

  it("refuses values that are not an address at all", () => {
    expect(safeMailtoUrl("not an email")).toBeNull();
    expect(safeMailtoUrl("a@b")).toBeNull();
    expect(safeMailtoUrl(null)).toBeNull();
    expect(safeMailtoUrl("")).toBeNull();
  });
});
