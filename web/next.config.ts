import path from "node:path";
import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";
import createMDX from "@next/mdx";
import { withSentryConfig } from "@sentry/nextjs";

const withNextIntl = createNextIntlPlugin("./src/i18n/request.ts");

// The guide articles are .mdx files under src/content, imported by the guide routes rather than
// routed to directly — so `pageExtensions` stays untouched and no stray markdown file under app/
// can become a page.
//
// **The articles are plain CommonMark: no GFM.** remark-gfm was tried and does not take effect on
// this toolchain — under `next build` (Turbopack) the .mdx still compiles, but nothing passed as
// `options.remarkPlugins` reaches the pipeline, with or without TURBOPACK set and in either plugin
// order. It fails silently, which is the dangerous part: a pipe table renders as literal pipes and
// nothing warns you. So when writing an article, no tables, no strikethrough, no task lists — use a
// list instead. `src/mdx-components.tsx` still styles `table`/`th`/`td` for the day this is fixed.
const withMDX = createMDX({});

// Both are baked in at build time (see web/Dockerfile) — the browser has to be allowed to reach
// whichever origins they point at, so the policy below is derived from them rather than
// hardcoding a production hostname that would silently break local/preview builds.
function originOf(value: string | undefined): string | null {
  if (!value) return null;
  try {
    return new URL(value).origin;
  } catch {
    return null;
  }
}

const apiOrigin = originOf(process.env.NEXT_PUBLIC_API_BASE_URL) ?? "http://localhost:5151";
const sentryOrigin = originOf(process.env.NEXT_PUBLIC_SENTRY_DSN);

// SignalR (/hubs/import-progress) upgrades to a WebSocket against the same origin as the API, and
// ws:/wss: are their own CSP scheme — connect-src 'self' plus the https origin does not cover them.
const apiWebSocketOrigin = apiOrigin.replace(/^http/, "ws");

// Ordered by how much each one actually buys us here, not alphabetically:
//
//  - connect-src is the important one. Access and refresh tokens live in localStorage
//    (web/src/lib/api/tokenStorage.ts), so the cheapest possible exfiltration for injected script
//    is a fetch to an attacker's host; this reduces the reachable set to our own API and Sentry.
//  - frame-ancestors closes clickjacking on the state-changing screens (suggestion confirm,
//    account deletion), which had no protection at all before.
//  - base-uri stops an injected <base> tag from repointing every relative script URL, which is a
//    standard way to turn a markup injection into script execution.
//  - object-src/form-action remove two more legacy escape hatches.
//
// script-src still needs 'unsafe-inline': Next.js emits inline bootstrap/hydration scripts
// (self.__next_f.push(...)) with no nonce unless we generate one per request in the proxy and read
// it back in the root layout — which opts every page, including the statically-rendered landing and
// help pages, into dynamic rendering. Deliberately deferred; see DECISIONS.md. That means this CSP
// hardens exfiltration and framing rather than injection itself, and the sanitization at the one
// dangerouslySetInnerHTML call site (JobDescriptionCard) is still the primary XSS control.
//
// The PayTR checkout is the one deliberate exception (DECISIONS.md 2026-09-15):
//  - frame-src https://www.paytr.com is in the *global* policy, because the checkout is reached by
//    a client-side navigation and a CSP belongs to the document, not the route — a per-route
//    header for /pro/checkout only applies on a full page load and left the frame blocked after a
//    click from /pro. Allowing PayTR as a frame source on every page costs nothing: it only says
//    which origins may be embedded, and the card form lives inside that frame, never on our origin.
//  - script-src does NOT widen: PayTR's iframe-resizer parent script is served from our own
//    origin (public/vendor/paytr-iframeResizer.min.js), so 'self' covers it.
//  - /{locale}/pro/return/* may be rendered *inside* that frame when PayTR navigates it to our
//    merchant_ok_url instead of the top window, so that route alone allows paytr.com as a frame
//    ancestor and drops the legacy X-Frame-Options (which cannot express an allow-list). The page
//    holds no state and only redirects the top window to the result page.
const PAYTR_ORIGIN = "https://www.paytr.com";

function buildCsp(overrides: Partial<Record<string, string>> = {}): string {
  const directives: Record<string, string> = {
    "default-src": "'self'",
    "script-src": "'self' 'unsafe-inline'",
    "style-src": "'self' 'unsafe-inline'",
    "img-src": "'self' data: blob:",
    "font-src": "'self' data:",
    "frame-src": PAYTR_ORIGIN,
    "connect-src": `'self' ${apiOrigin} ${apiWebSocketOrigin}${sentryOrigin ? ` ${sentryOrigin}` : ""}`,
    "frame-ancestors": "'none'",
    "base-uri": "'self'",
    "form-action": "'self'",
    "object-src": "'none'",
    ...overrides,
  };
  return Object.entries(directives)
    .map(([key, value]) => `${key} ${value}`)
    .join("; ");
}

const contentSecurityPolicy = buildCsp();

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
  { key: "Content-Security-Policy", value: contentSecurityPolicy },
  // frame-ancestors above already covers this for anything modern; kept for older browsers that
  // understand the legacy header but not the directive.
  { key: "X-Frame-Options", value: "DENY" },
  ...commonSecurityHeaders,
];

const paytrReturnSecurityHeaders = [
  { key: "Content-Security-Policy", value: buildCsp({ "frame-ancestors": `'self' ${PAYTR_ORIGIN}` }) },
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
      ],
      afterFiles: [],
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
export default withSentryConfig(withNextIntl(withMDX(nextConfig)), {
  org: process.env.SENTRY_ORG,
  project: process.env.SENTRY_PROJECT,
  authToken: process.env.SENTRY_AUTH_TOKEN,
  silent: !process.env.CI,
  widenClientFileUpload: true,
});
