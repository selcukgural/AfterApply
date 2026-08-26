# syntax=docker/dockerfile:1
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# NEXT_PUBLIC_* vars must be present as real env vars during `next build` —
# Next.js inlines them into the client bundle at build time, a runtime
# `environment:` on the final container has no effect on already-built
# client JS. ARG -> ENV is the standard Docker pattern to thread a
# build-time value through to the build command below.
ARG NEXT_PUBLIC_API_BASE_URL
ENV NEXT_PUBLIC_API_BASE_URL=${NEXT_PUBLIC_API_BASE_URL}
ARG NEXT_PUBLIC_SENTRY_DSN
ENV NEXT_PUBLIC_SENTRY_DSN=${NEXT_PUBLIC_SENTRY_DSN}
# Source-map upload only (readable stack traces in the Sentry UI) — all
# three optional, `withSentryConfig` (next.config.ts) already no-ops the
# upload step (not the build) when SENTRY_AUTH_TOKEN is empty. Unlike the
# two ARGs above, SENTRY_AUTH_TOKEN is a real credential: it stays out of
# the final pushed image (this `build` stage's layers aren't copied into
# the `runtime` stage below, and `docker build` without `--target` only
# tags/pushes the last stage) but IS visible in this build stage's local
# layer cache on whichever machine runs the build (e.g. a GitHub Actions
# runner, discarded when the job ends) — accepted for now since source
# maps are optional; harden with a BuildKit `RUN --mount=type=secret`
# instead of ARG if that residual exposure ever matters.
ARG SENTRY_ORG
ENV SENTRY_ORG=${SENTRY_ORG}
ARG SENTRY_PROJECT
ENV SENTRY_PROJECT=${SENTRY_PROJECT}
ARG SENTRY_AUTH_TOKEN
ENV SENTRY_AUTH_TOKEN=${SENTRY_AUTH_TOKEN}
RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public
EXPOSE 3000
ENTRYPOINT ["node", "server.js"]
