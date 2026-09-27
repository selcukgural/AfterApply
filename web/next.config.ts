import path from "node:path";
import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";
import { withSentryConfig } from "@sentry/nextjs";
import { UNSERVED_ROOT_FILE_REWRITE } from "./src/lib/http/canonicalHost";
import { originOf } from "./src/lib/http/contentSecurityPolicy";

const withNextIntl = createNextIntlPlugin("./src/i18n/request.ts");

// The API origin is baked in at build time (see web/Dockerfile); the blog-image rewrite below
// proxies to it.
const apiOrigin = originOf(process.env.NEXT_PUBLIC_API_BASE_URL) ?? "http://localhost:5151";

// Content-Security-Policy is not set here: it carries a per-request nonce, so proxy.ts builds it
// for every rendered page (src/lib/http/contentSecurityPolicy.ts). These are the headers that are
// the same on every response.
const commonSecurityHeaders = [
  { key: "X-Content-Type-Options", value: "nosniff" },
  // Password-reset links arrive as ?email=&token= query strings — strict-origin-when-cross-origin
  // keeps that token out of the Referer header on any outbound navigation from the reset page.
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  // Nothing in this app uses any of these; denying them keeps an injected iframe or script from
  // prompting the user for hardware access under our origin's name. PayTR's card form does not
  // use the Payment Request API, so payment=() stays denied on the checkout too.
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), payment=()" },
  // Two years, the minimum for HSTS preload eligibility. Safe to assert unconditionally: this
  // header is only ever set on responses the browser received over https to begin with.
  { key: "Strict-Transport-Security", value: "max-age=63072000; includeSubDomains; preload" },
];

const securityHeaders = [
  // The CSP's frame-ancestors already covers this for anything modern; kept for older browsers that
  // understand the legacy header but not the directive.
  { key: "X-Frame-Options", value: "DENY" },
  ...commonSecurityHeaders,
];

// The PayTR return page may be framed by paytr.com (its CSP says so, see
// contentSecurityPolicy.ts), so it must not carry X-Frame-Options, which cannot express an
// allow-list.
const paytrReturnSecurityHeaders = [
  ...commonSecurityHeaders,
];

const nextConfig: NextConfig = {
  output: "standalone",
  turbopack: {
    root: path.join(__dirname),
  },
  // The CV scan's English address. The route directory is the Turkish slug (the phrase the page is
  // built for); /en/cv-scan is served from it by rewrite, and the wrong-locale spellings are
  // redirected from proxy.ts — see src/lib/cvScan/path.ts.
  async rewrites() {
    return {
      beforeFiles: [
        { source: "/en/cv-scan", destination: "/en/cv-tarama" },
        { source: "/en/cv-scan/score/:card", destination: "/en/cv-tarama/puan/:card" },
        // The about page, the same way (src/lib/about/path.ts).
        { source: "/en/about", destination: "/en/hakkimizda" },
        // The offer comparison, the same way (src/lib/offerCompare/path.ts).
        { source: "/en/offer-comparison", destination: "/en/teklif-karsilastirma" },
        // A shared application-flow card (src/lib/flowCard/path.ts).
        { source: "/en/flow/:card", destination: "/en/akis/:card" },
        // Blog images (2026-09-19). The post's HTML stores the image as the relative path the API
        // serves it at, and this proxies that path to the API — so the stored markup carries no
        // hostname (the same HTML works on a laptop, a preview and production), the CSP's
        // img-src stays 'self', and a share card's image URL is on our own origin. The API sets
        // the cache headers (a year, immutable, for a published post's image); Next passes them on.
        { source: "/api/blog/media/:id", destination: `${apiOrigin}/api/blog/media/:id` },
      ],
      // A root-level file nothing serves (/llms.txt) gets the 404, not a 500 — see canonicalHost.ts.
      afterFiles: [UNSERVED_ROOT_FILE_REWRITE],
      fallback: [],
    };
  },
  async headers() {
    // The PayTR return route is excluded from the catch-all outright: it must not carry
    // X-Frame-Options at all, and a later matching entry can override a header but not remove it.
    return [
      { source: "/:path((?!(?:tr|en)/pro/return/).*)", headers: securityHeaders },
      { source: "/:locale(tr|en)/pro/return/:path*", headers: paytrReturnSecurityHeaders },
    ];
  },
};

// Source-map upload (org/project/authToken) is only wired up once a real
// Sentry project exists (Sprint 13, DECISIONS.md) — until then this no-ops
// safely: the plugin prints a notice and skips upload instead of failing
// the build when SENTRY_AUTH_TOKEN is unset (confirmed getsentry/sentry-javascript
// behavior, not a guess), so error reporting itself (instrumentation-client.ts /
// sentry.server.config.ts / sentry.edge.config.ts) works even without it —
// only readable (unminified) stack traces in the Sentry UI wait on this.
export default withSentryConfig(withNextIntl(nextConfig), {
  org: process.env.SENTRY_ORG,
  project: process.env.SENTRY_PROJECT,
  authToken: process.env.SENTRY_AUTH_TOKEN,
  silent: !process.env.CI,
  widenClientFileUpload: true,
});
