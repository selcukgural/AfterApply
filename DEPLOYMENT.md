# Deployment

Sprint 7 scope: a locally-verifiable "prod-like" Docker Compose profile,
**not** a real cloud deployment. Cloud provider is now decided (see
`DECISIONS.md`, "5. Cloud provider — DECIDED": Vercel + Google Cloud
Run + Neon + Upstash) but not yet wired up — that's Sprint 13 work.
This document remains the stand-in the Sprint 7 DoD requires
("prod-benzeri bir ortamda doğrulanabilir") until Sprint 13 replaces it
with real deployment instructions.

## Running the prod profile locally

1. `cp .env.prod.example .env.prod` and fill in real values (see below —
   do not reuse the dev `.env`'s placeholder secrets).
2. ```bash
   podman compose --env-file .env.prod -f docker-compose.yml -f docker-compose.prod.yml up --build
   ```
   (or `podman compose ...` — see README's Podman notes for
   `DOCKER_HOST`/Ryuk caveats, which don't apply here since this is a
   plain Compose run, not Testcontainers)
3. Apply migrations (not automatic — see below):
   ```bash
   dotnet ef database update --project src/AfterApply.Infrastructure --startup-project src/AfterApply.Api \
     --connection "Host=localhost;Port=5434;Database=afterapply;Username=afterapply;Password=<from .env.prod>"
   ```
   (or run it from inside the `api` container/a one-off `dotnet ef` container if you don't have the SDK on the host — this repo doesn't yet automate that, tracked as a follow-up)
4. Frontend at `http://localhost:3000`, API at `http://localhost:8080/health`.

## What changes vs. the dev `docker-compose.yml`

- `postgres`/`redis` no longer publish host ports — the dev file exposes
  them for local `psql`/`redis-cli` access; nothing outside the Compose
  network needs them in this profile.
- `api` keeps its host port — the browser calls it directly, since there
  is no reverse proxy in front of it yet (see below).
- A `web` service is added (`web/Dockerfile`, Next.js standalone build).
- All secrets come from `.env.prod` (git-ignored) instead of the dev
  file's placeholder defaults.

## `.env.prod` fields

See `.env.prod.example` for the full list with inline comments:
`POSTGRES_DB`/`POSTGRES_USER`/`POSTGRES_PASSWORD` (a real generated
password, not the dev placeholder), `JWT_SIGNING_KEY` (`openssl rand
-base64 48`), `WEB_ORIGIN` (added to the API's CORS allow-list),
`API_ORIGIN` (baked into the frontend build as
`NEXT_PUBLIC_API_BASE_URL` — the origin the *browser* calls the API on,
not an internal Compose service name).

## Migrations

`Program.cs` does **not** call `Database.Migrate()` on startup, in this
profile or in dev — migrations are always an explicit, separate step
(`dotnet ef database update`), never silently auto-applied on every
container restart. This is a deliberate choice, not an oversight — see
step 3 above for how to run it against this profile's Postgres.

## What's still missing for a real cloud deployment

This profile deliberately stops short of being cloud-ready:

- **No reverse proxy / TLS.** A real deployment needs Caddy/Nginx (or a
  cloud load balancer) in front of `api` and `web`, terminating TLS on a
  real domain. Not built here — there's no domain/cloud target yet to
  configure it against, and building one speculatively would be
  unverifiable dead weight per this project's YAGNI rule (spec §31.20).
- **No managed Postgres/Redis.** This profile runs both in containers
  with a local volume — a real deployment should point
  `ConnectionStrings__Postgres`/`ConnectionStrings__Redis` at managed
  instances (Neon / Upstash, per the Sprint 13 decision) instead.
- **No secrets manager.** `.env.prod` is a plain file; a real deployment
  should use the target cloud's secrets manager instead.
- **Cloud provider decided, not yet wired up** — Vercel (`web`) + Google
  Cloud Run (`api`) + Neon (Postgres) + Upstash (Redis), see
  `DECISIONS.md` §5. The container images built here
  (`src/AfterApply.Api/Dockerfile`, `web/Dockerfile`) are the deployable
  artifacts either way — no further image changes should be needed,
  only the hosting/networking/secrets layer around them (Sprint 13).
