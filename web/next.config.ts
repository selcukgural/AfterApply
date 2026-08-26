import path from "node:path";
import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";
import { withSentryConfig } from "@sentry/nextjs";

const withNextIntl = createNextIntlPlugin("./src/i18n/request.ts");

const nextConfig: NextConfig = {
  output: "standalone",
  turbopack: {
    root: path.join(__dirname),
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
