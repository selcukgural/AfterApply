import * as Sentry from "@sentry/nextjs";
import { scrubSentryBreadcrumb, scrubSentryEvent } from "@/lib/privacy/sentryScrub";

// Empty NEXT_PUBLIC_SENTRY_DSN disables the SDK (no events sent) rather than
// throwing — same "stays inert until real values are set" pattern as the
// backend's Sentry:Dsn (see DECISIONS.md "Sprint 13").
Sentry.init({
  dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
  environment: process.env.NODE_ENV,
  // Reset tokens, OAuth codes and hub tickets travel in URLs; none of them goes to Sentry.
  beforeSend: scrubSentryEvent,
  beforeBreadcrumb: scrubSentryBreadcrumb,
});

export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
