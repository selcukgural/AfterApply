# syntax=docker/dockerfile:1
# Base images are pinned by digest (tag kept for readability); Dependabot's docker updates move
# the digest forward.
FROM node:24-alpine@sha256:ebfe2f90462722a7a4de65e91990e97fe0d401c70e0e762c5b53302f905ec1c1 AS build
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
# upload step (not the build) when SENTRY_AUTH_TOKEN is empty.
# SENTRY_AUTH_TOKEN is a real credential, so it is a BuildKit secret
# (`docker build --secret id=sentry_auth_token,env=SENTRY_AUTH_TOKEN`), not
# an ARG: a secret mount exists for this one RUN and is written into no
# layer, where an ARG/ENV would sit in the build stage's layer metadata on
# whichever machine ran the build. Without the secret the build still
# succeeds, minus the upload.
ARG SENTRY_ORG
ENV SENTRY_ORG=${SENTRY_ORG}
ARG SENTRY_PROJECT
ENV SENTRY_PROJECT=${SENTRY_PROJECT}
RUN --mount=type=secret,id=sentry_auth_token,required=false \
    SENTRY_AUTH_TOKEN="$(cat /run/secrets/sentry_auth_token 2>/dev/null || true)" npm run build

FROM node:24-alpine@sha256:ebfe2f90462722a7a4de65e91990e97fe0d401c70e0e762c5b53302f905ec1c1 AS runtime
WORKDIR /app
ENV NODE_ENV=production
# --chown so the standalone server owns its own tree: Next.js writes into .next/cache at runtime,
# and a root-owned copy would leave that unwritable once we drop privileges below. The base image
# ships this "node" user (uid 1000) but, like the .NET one, still defaults to root.
COPY --from=build --chown=node:node /app/.next/standalone ./
COPY --from=build --chown=node:node /app/.next/static ./.next/static
COPY --from=build --chown=node:node /app/public ./public
EXPOSE 3000
USER node
ENTRYPOINT ["node", "server.js"]
