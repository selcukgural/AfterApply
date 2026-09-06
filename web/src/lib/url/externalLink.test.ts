import { describe, expect, it } from "vitest";
import { externalUrlLabel, safeExternalUrl } from "@/lib/url/externalLink";

describe("safeExternalUrl", () => {
  it("passes through ordinary http(s) links", () => {
    expect(safeExternalUrl("https://www.oplog.com/")).toBe("https://www.oplog.com/");
    expect(safeExternalUrl("http://example.com/jobs")).toBe("http://example.com/jobs");
  });

  it("refuses script-bearing schemes, which is the whole point of the helper", () => {
    // These reach us as stored strings: the extension scrapes hrefs off a job page, and a user can
    // type anything into the job URL field. Rendered as an href, either one runs on click.
    expect(safeExternalUrl("javascript:alert(1)")).toBeNull();
    expect(safeExternalUrl("data:text/html,<script>alert(1)</script>")).toBeNull();
    expect(safeExternalUrl("vbscript:msgbox(1)")).toBeNull();
  });

  it("refuses relative and unparsable values instead of linking back into our own app", () => {
    expect(safeExternalUrl("/settings")).toBeNull();
    expect(safeExternalUrl("oplog.com")).toBeNull();
    expect(safeExternalUrl("not a url")).toBeNull();
  });

  it("treats missing values as no link", () => {
    expect(safeExternalUrl(null)).toBeNull();
    expect(safeExternalUrl(undefined)).toBeNull();
    expect(safeExternalUrl("")).toBeNull();
  });
});

describe("externalUrlLabel", () => {
  it("shows the host without the www, not the full URL", () => {
    expect(externalUrlLabel("https://www.borusanlojistik.com/tr")).toBe("borusanlojistik.com");
    expect(externalUrlLabel("https://albarakatech.com/tr.html")).toBe("albarakatech.com");
  });

  it("falls back to the raw string rather than rendering an empty label", () => {
    expect(externalUrlLabel("oplog.com")).toBe("oplog.com");
  });
});
