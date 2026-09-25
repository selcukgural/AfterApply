import { describe, expect, it } from "vitest";
// @ts-expect-error — Next's bundled copy ships no types; it is the matcher the rewrite actually runs through.
import { match } from "next/dist/compiled/path-to-regexp";
import { UNSERVED_ROOT_FILE_REWRITE, apexRedirectUrl, isFileRequest, stripIndexHtml } from "./canonicalHost";

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

describe("stripIndexHtml", () => {
  // Search Console: http://www.ekariyerim.com/index.html — a crawler's guess at the front page.
  it("folds the crawler's guessed index.html onto the root", () => {
    expect(stripIndexHtml("/index.html")).toBe("/");
    expect(stripIndexHtml("/tr/index.html")).toBe("/tr");
    expect(apexRedirectUrl("www.ekariyerim.com", stripIndexHtml("/index.html"), "")).toBe("https://ekariyerim.com/");
  });

  it("leaves every other path alone", () => {
    expect(stripIndexHtml("/")).toBe("/");
    expect(stripIndexHtml("/tr")).toBe("/tr");
    expect(stripIndexHtml("/sitemap.xml")).toBe("/sitemap.xml");
    expect(stripIndexHtml("/guide/index.html.bak")).toBe("/guide/index.html.bak");
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

describe("UNSERVED_ROOT_FILE_REWRITE", () => {
  const claims = (path: string) => Boolean(match(UNSERVED_ROOT_FILE_REWRITE.source)(path));

  // These answered 500 before (2026-09-25): the file name became the locale.
  it("catches a dotted first segment, with or without more after it", () => {
    expect(claims("/llms.txt")).toBe(true);
    expect(claims("/ads.txt")).toBe(true);
    expect(claims("/favicon.ico")).toBe(true);
    expect(claims("/.well-known/security.txt")).toBe(true);
  });

  // afterFiles also sees locale pages that no static file claimed; it must leave all of them to
  // their routes, or every dynamic page (a company, a blog post) would become the 404.
  it("leaves every locale path alone, dotted or not", () => {
    expect(claims("/tr")).toBe(false);
    expect(claims("/en/help/chrome-extension")).toBe(false);
    expect(claims("/tr/companies/acme")).toBe(false);
    expect(claims("/tr/guide/is-basvuru-takip-sablonu.xlsx")).toBe(false);
  });

  it("lands on a locale address, where the site's own 404 page renders", () => {
    expect(UNSERVED_ROOT_FILE_REWRITE.destination).toMatch(/^\/(tr|en)\//);
  });
});
