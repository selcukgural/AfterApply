import path from "node:path";
import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";
import { withSentryConfig } from "@sentry/nextjs";

const withNextIntl = createNextIntlPlugin("./src/i18n/request.ts");

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
const contentSecurityPolicy = [
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline'",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob:",
  "font-src 'self' data:",
  `connect-src 'self' ${apiOrigin} ${apiWebSocketOrigin}${sentryOrigin ? ` ${sentryOrigin}` : ""}`,
  "frame-ancestors 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "object-src 'none'",
].join("; ");

const securityHeaders = [
  { key: "Content-Security-Policy", value: contentSecurityPolicy },
  // frame-ancestors above already covers this for anything modern; kept for older browsers that
  // understand the legacy header but not the directive.
  { key: "X-Frame-Options", value: "DENY" },
  { key: "X-Content-Type-Options", value: "nosniff" },
  // Password-reset links arrive as ?email=&token= query strings — strict-origin-when-cross-origin
  // keeps that token out of the Referer header on any outbound navigation from the reset page.
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  // Nothing in this app uses any of these; denying them keeps an injected iframe or script from
  // prompting the user for hardware access under our origin's name.
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), payment=()" },
  // Two years, the minimum for HSTS preload eligibility. Safe to assert unconditionally: this
  // header is only ever set on responses the browser received over https to begin with.
  { key: "Strict-Transport-Security", value: "max-age=63072000; includeSubDomains; preload" },
];

const nextConfig: NextConfig = {
  output: "standalone",
  turbopack: {
    root: path.join(__dirname),
  },
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders }];
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
