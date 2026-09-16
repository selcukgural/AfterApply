import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Source-scan contract for the PayTR pieces that live in configuration rather than code: the CSP
 * relaxations must stay scoped to the two routes, and the return page must keep its frame
 * bust-out. A refactor that widens frame-src to every page, or drops the return route from the
 * catch-all exclusion, would pass every other test and only fail in a customer's browser.
 */
const root = path.resolve(__dirname, "../../..");
const nextConfig = readFileSync(path.join(root, "next.config.ts"), "utf8");
const returnPage = readFileSync(path.join(root, "src/app/[locale]/pro/return/[orderId]/page.tsx"), "utf8");
const frame = readFileSync(path.join(root, "src/components/pro/PayTrFrame.tsx"), "utf8");

describe("PayTR content security policy", () => {
  it("allows paytr.com as a frame source everywhere and never as a script source", () => {
    expect(nextConfig).toContain('const PAYTR_ORIGIN = "https://www.paytr.com"');
    // In the global policy: the checkout is reached by client-side navigation, so the document's
    // CSP is whatever page was loaded first.
    expect(nextConfig).toContain('"frame-src": PAYTR_ORIGIN');
    expect(nextConfig).toContain(`"script-src": "'self' 'unsafe-inline'"`);
    expect(nextConfig).not.toMatch(/script-src[^\n]*PAYTR_ORIGIN/);
    expect(nextConfig).not.toMatch(/script-src[^\n]*paytr\.com/);
  });

  it("lets paytr.com frame the return route and keeps X-Frame-Options off it", () => {
    expect(nextConfig).toContain("`'self' ${PAYTR_ORIGIN}`");
    expect(nextConfig).toContain('source: "/:path((?!(?:tr|en)/pro/return/).*)", headers: securityHeaders');
    expect(nextConfig).toContain('source: "/:locale(tr|en)/pro/return/:path*", headers: paytrReturnSecurityHeaders');
    const returnBlock = nextConfig.slice(nextConfig.indexOf("const paytrReturnSecurityHeaders"), nextConfig.indexOf("const nextConfig"));
    expect(returnBlock).not.toContain("X-Frame-Options");
  });

  it("keeps the return page a pure redirect that breaks out of a frame", () => {
    expect(returnPage).toContain("window.top !== window.self");
    expect(returnPage).toContain("window.top.location.replace(target)");
    expect(returnPage).toContain("/pro/orders/");
    expect(returnPage).not.toContain("apiFetch");
    expect(returnPage).not.toContain("paymentsApi");
  });

  it("embeds the payment frame the way PayTR documents it, with the resizer served from our origin", () => {
    expect(frame).toContain('const RESIZER_SRC = "/vendor/paytr-iframeResizer.min.js"');
    expect(existsSync(path.join(root, "public/vendor/paytr-iframeResizer.min.js"))).toBe(true);
    expect(frame).toContain('const IFRAME_ID = "paytriframe"');
    expect(frame).toContain("onError={() => setResizerFailed(true)}");
  });
});
