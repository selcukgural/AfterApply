import * as Sentry from "@sentry/nextjs";

// Empty NEXT_PUBLIC_SENTRY_DSN disables the SDK (no events sent) rather than
// throwing — same "stays inert until real values are set" pattern as the
// backend's Sentry:Dsn (see DECISIONS.md "Sprint 13").
Sentry.init({
  dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
  environment: process.env.NODE_ENV,
});

export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
