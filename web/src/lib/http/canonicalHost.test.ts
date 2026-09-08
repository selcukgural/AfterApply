import { describe, expect, it } from "vitest";
import { apexRedirectUrl, isFileRequest } from "./canonicalHost";

describe("apexRedirectUrl", () => {
  it("sends a www request to the apex, keeping path and query", () => {
    expect(apexRedirectUrl("www.ekariyerim.com", "/tr/guide/is-basvuru-takip-excel-sablonu", "")).toBe(
      "https://ekariyerim.com/tr/guide/is-basvuru-takip-excel-sablonu",
    );
    expect(apexRedirectUrl("www.ekariyerim.com", "/tr/login", "?next=%2Ftr%2Fdashboard")).toBe(
      "https://ekariyerim.com/tr/login?next=%2Ftr%2Fdashboard",
    );
  });

  it("covers the two files a crawler asks for by URL", () => {
    expect(apexRedirectUrl("www.ekariyerim.com", "/sitemap.xml", "")).toBe("https://ekariyerim.com/sitemap.xml");
    expect(apexRedirectUrl("www.ekariyerim.com", "/robots.txt", "")).toBe("https://ekariyerim.com/robots.txt");
  });

  // The apex must never redirect: it is the target, and a rule that matched it would loop.
  it("leaves the canonical host alone", () => {
    expect(apexRedirectUrl("ekariyerim.com", "/tr", "")).toBeNull();
    expect(apexRedirectUrl("localhost:3000", "/tr", "")).toBeNull();
    expect(apexRedirectUrl("afterapply-web-abc123.a.run.app", "/tr", "")).toBeNull();
  });

  it("matches the prefix rather than the substring", () => {
    expect(apexRedirectUrl("wwwx.ekariyerim.com", "/tr", "")).toBeNull();
    expect(apexRedirectUrl("notwww.ekariyerim.com", "/tr", "")).toBeNull();
    expect(apexRedirectUrl("api.ekariyerim.com", "/tr", "")).toBeNull();
  });

  it("is case-insensitive, because the Host header need not be lowercase", () => {
    expect(apexRedirectUrl("WWW.Ekariyerim.COM", "/tr", "")).toBe("https://ekariyerim.com/tr");
  });

  it("drops the port and always lands on https", () => {
    expect(apexRedirectUrl("www.ekariyerim.com:443", "/tr", "")).toBe("https://ekariyerim.com/tr");
    expect(apexRedirectUrl("www.ekariyerim.com:80", "/tr", "")).toBe("https://ekariyerim.com/tr");
  });

  it("returns null rather than throwing when there is no Host header", () => {
    expect(apexRedirectUrl(null, "/tr", "")).toBeNull();
    expect(apexRedirectUrl(undefined, "/tr", "")).toBeNull();
    expect(apexRedirectUrl("", "/tr", "")).toBeNull();
  });

  it("does not turn a bare \"www.\" into a redirect to nowhere", () => {
    expect(apexRedirectUrl("www.", "/tr", "")).toBeNull();
  });
});

describe("isFileRequest", () => {
  // Without this the locale middleware rewrites /sitemap.xml to /tr/sitemap.xml, and Google fetches
  // a 404 from the exact URL robots.txt advertises.
  it("recognises the files that must skip the locale middleware", () => {
    expect(isFileRequest("/sitemap.xml")).toBe(true);
    expect(isFileRequest("/robots.txt")).toBe(true);
    expect(isFileRequest("/guide/is-basvuru-takip-sablonu.xlsx")).toBe(true);
  });

  it("treats every page path as a page", () => {
    expect(isFileRequest("/")).toBe(false);
    expect(isFileRequest("/tr")).toBe(false);
    expect(isFileRequest("/tr/guide")).toBe(false);
    expect(isFileRequest("/tr/guide/kariyer-net-basvurularim-nerede")).toBe(false);
    expect(isFileRequest("/en/help/chrome-extension")).toBe(false);
  });
});
