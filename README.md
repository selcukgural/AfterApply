# AfterApply

Job Application Tracker + Personal Analytics. See `afterapply-intelligence-platform-plan.md`
for the product/technical spec, `DEVELOPMENT_PLAN.md` for the sprint roadmap,
and `DECISIONS.md` for architecture/technical decisions.

**Status: Sprint 5 (LinkedIn Data Export Import).** Backend: auth,
Application CRUD/status/timeline, paginated+filterable application list,
dashboard summary counts, `GET /api/analytics/overview` (response/interview/
offer/rejection/ghosting rates, average/median response time, status
distribution), CORS, `POST /api/imports/csv` (generic CSV upload with
auto-detected/overridable column mapping, validation + per-row error
report, dedup/idempotent import summary), `POST /api/imports/linkedin`
(LinkedIn Data Export ZIP upload — discovers `Job Applications*.csv` files,
dedups by LinkedIn job id / URL / company+title+date, aggregates into one
import summary) and `GET /api/imports/{id}`. Frontend (`web/`, Next.js):
login/register, dashboard (stat tiles + analytics rates + response-time
card + status-distribution chart), application list/detail/create/edit,
status changes, timeline — no import UI yet (Sprint 4/5 are backend-only
per `DEVELOPMENT_PLAN.md`). No reminders yet — see `DEVELOPMENT_PLAN.md`
for what's next.

## Architecture

Clean Architecture, layer-first modular monolith:

```
Domain            (no project references)
Application  ──►  Domain
Infrastructure ──► Application, Domain
Api          ──►  Infrastructure, Application, Domain
```

Modules (Applications, Companies, Jobs, Identity, Imports, Analytics,
Notifications, CompanyIntelligence) live as namespaces/folders inside each
layer, not as separate projects — see `DECISIONS.md` #1. Layer dependency
direction is enforced by NetArchTest (`tests/AfterApply.UnitTests/Architecture`).

## Prerequisites

- .NET SDK `10.0.105`+ (see `global.json`) — check with `dotnet --version`
- PostgreSQL (native, or via container)
- Redis (native, or via container)
- For the container path: Podman (or Docker) + `docker-compose` on PATH.
  This repo was verified against Podman — no Docker Desktop required.

## Quick start — native local Postgres/Redis (recommended for day-to-day dev)

> ⚠️ **Port conflicts:** if you run other local projects with their own
> Postgres/Redis containers, the default ports (5432/6379) may already be
> taken by something else. Check with `lsof -nP -iTCP:5432 -sTCP:LISTEN` /
> `-iTCP:6379` before assuming a port is free, and adjust the connection
> strings below accordingly.

1. Start Postgres (e.g. `brew services start postgresql@17`) and make sure a
   Redis instance is reachable (e.g. `redis-server --port 6379 --daemonize yes`,
   or a different port if 6379 is already in use on your machine).
2. `createdb afterapply_dev`
3. `dotnet user-secrets init --project src/AfterApply.Api` (one-time)
4. ```bash
   dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=afterapply_dev;Username=$(whoami)" --project src/AfterApply.Api
   dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project src/AfterApply.Api
   dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/AfterApply.Api
   ```
   (adjust ports if you hit a conflict per the warning above)
5. Apply migrations: `dotnet ef database update --project src/AfterApply.Infrastructure --startup-project src/AfterApply.Api`
6. `dotnet build AfterApply.slnx`
7. `dotnet run --project src/AfterApply.Api --launch-profile http` (always
   use the `http` launch profile — it's the source of truth for the dev
   port, `5151`; an ad-hoc `--urls` override will silently break the
   frontend's `.env.local`, which points at `5151`)
8. `curl -i http://localhost:5151/health` → expect `200 Healthy`

### Trying the API

```bash
curl -X POST http://localhost:5151/api/auth/register -H "Content-Type: application/json" -d \
  '{"email":"you@example.com","password":"P@ssw0rd123!","firstName":"You","lastName":"There"}'
# → { "accessToken": "...", "refreshToken": "...", ... }

curl -X POST http://localhost:5151/api/applications -H "Authorization: Bearer <accessToken>" \
  -H "Content-Type: application/json" -d \
  '{"companyName":"Acme","jobTitle":"Backend Engineer","employmentType":"FullTime","appliedAt":"2026-08-20T10:00:00Z"}'

# Generic CSV import — required columns auto-detected via TR/EN alias table
# (Company/Şirket, Title/Pozisyon, Applied At/Tarih); Status/Job URL/Location
# optional. See DECISIONS.md "Sprint 4" for the alias table and dedup rules.
curl -X POST http://localhost:5151/api/imports/csv -H "Authorization: Bearer <accessToken>" \
  -F "file=@applications.csv;type=text/csv"
# → { "id": "...", "totalRecords": N, "newApplications": N, "duplicateRecords": N,
#     "invalidRecords": N, "errors": [{ "rowNumber": ..., "errorMessage": ... }] }
# Re-uploading the same file is idempotent (0 newApplications on the second run).

# LinkedIn Data Export import — upload the whole "Get a copy of your data"
# export ZIP; only Jobs/Job Applications*.csv entries are read (others are
# skipped without decompressing). Dedups by LinkedIn job id (extracted from
# the job URL) first, then job URL, then company+title+applied date. See
# DECISIONS.md "Sprint 5".
curl -X POST http://localhost:5151/api/imports/linkedin -H "Authorization: Bearer <accessToken>" \
  -F "file=@linkedin_export.zip;type=application/zip"
```

See `/api/auth/*`, `/api/users/me`, `/api/applications/*`, `/api/imports/*`
in `src/AfterApply.Api/Endpoints` for the full surface, or browse
`/openapi/v1.json` (Development only).

## Frontend (`web/`)

Next.js (App Router, TypeScript, Tailwind CSS 4). Requires the backend
running on `http://localhost:5151` (see above) with
`Cors:AllowedOrigins` including `http://localhost:3000` (already the
`appsettings.Development.json` default).

```bash
cd web
npm install                        # one-time
cp .env.local.example .env.local   # one-time — points at http://localhost:5151
npm run dev
```

Open `http://localhost:3000`. `npm run build` / `npm run lint` for a
production build / lint check.

## Container environment (podman compose / docker compose)

Uses offset host ports (**5434** for Postgres, **6382** for Redis) to reduce
the odds of colliding with a native Postgres/Redis or another local
project's containers — check for conflicts on your machine and adjust
`docker-compose.yml` if needed.

1. `cp .env.example .env` and fill in `POSTGRES_PASSWORD`
2. `podman compose up --build` (or `docker compose up --build`)
3. `curl -i http://localhost:8080/health`
4. `podman compose down` (add `-v` to also drop the postgres volume)

`docker-compose.yml` (dev) only runs Postgres/Redis/API — it does **not**
build a frontend container. To use the UI against this containerized
backend, run the frontend natively and point it at port 8080:
```bash
cd web
npm install
echo "NEXT_PUBLIC_API_BASE_URL=http://localhost:8080" > .env.local
npm run dev
```
(You still need `Cors:AllowedOrigins` in the API to include
`http://localhost:3000` — already the `appsettings.Development.json`
default.) For a fully containerized run (API **and** frontend, no native
`npm`/`dotnet` needed), use the prod-like overlay instead — see
`docker-compose.prod.yml` and `DEPLOYMENT.md`:
```bash
cp .env.prod.example .env.prod   # fill in real secrets, see DEPLOYMENT.md
podman compose --env-file .env.prod -f docker-compose.yml -f docker-compose.prod.yml up --build
# API:      http://localhost:8080
# Frontend: http://localhost:3000
```

## Running tests

- Fast, no container runtime needed: `dotnet test tests/AfterApply.UnitTests`
- Integration (needs a container runtime):
  ```bash
  # If using Podman instead of Docker Desktop, point Testcontainers at the podman socket:
  export DOCKER_HOST="unix://$(podman machine inspect --format '{{.ConnectionInfo.PodmanSocket.Path}}')"
  dotnet test tests/AfterApply.IntegrationTests
  ```
  If container startup hangs or fails under rootless Podman (Ryuk, the
  resource-reaper sidecar, is known to be flaky there), try
  `export TESTCONTAINERS_RYUK_DISABLED=true` — note this means stopped test
  containers may occasionally need manual cleanup:
  ```bash
  podman ps -a --format "{{.Names}} {{.Status}}"          # inspect what's left
  podman ps -a -q --filter "ancestor=docker.io/library/postgres:17-alpine" | xargs -r podman rm -f
  ```
  A `System.InvalidOperationException: Sequence contains no elements` /
  `HttpRequestException: Connection failed` error from a test's
  `InitializeAsync`/`DisposeAsync` is this same podman-socket flakiness, not
  a code bug — clean up leftover containers (above) and re-run. If it keeps
  happening, re-running with test-collection parallelism turned off isolates
  it further: `dotnet test tests/AfterApply.IntegrationTests -- xunit.parallelizeTestCollections=false`.
- Everything: `dotnet test AfterApply.slnx`

> **Workflow note (Claude Code sessions):** Podman-backed integration test
> runs in this environment are slow and intermittently flaky (Testcontainers
> ↔ podman socket connectivity hiccups, unrelated to the code under test —
> see DECISIONS.md's Sprint 6/7 notes), and re-running them after every small
> change burns a lot of time. During active development, only the unit
> tests (`tests/AfterApply.UnitTests`, no container runtime needed) are run
> continuously; the full integration suite (`tests/AfterApply.IntegrationTests`)
> is deferred and run once after a batch of changes is otherwise complete,
> not after each individual file edit.

## Manual smoke testing (browser)

With the backend and frontend both running (either the native or container
path above), a walkthrough of the main flows:

1. **Register**: `http://localhost:3000/register` — create an account,
   accept the consent checkbox (required to submit).
2. **Login**: `/login` with the same credentials.
3. **Dashboard**: `/` — stat tiles, analytics rates, response-time card,
   status-distribution chart (populates as applications are added).
4. **Applications**: `/applications` — create one (`/applications/new`),
   open its detail page, change its status, check the timeline updates.
5. **Import**: from `/applications`, try a CSV upload (`POST
   /api/imports/csv`, any file with Company/Title/Applied At columns) or a
   LinkedIn Data Export ZIP (`POST /api/imports/linkedin`) — check the
   import summary and that re-uploading the same file reports 0 new
   applications (idempotency).
6. **Privacy page**: `/privacy` — static page, linked from the register
   consent checkbox.
7. **Settings**: `/settings` (requires login) — account data export
   (download), account deletion (asks for password), and the "E-posta
   Entegrasyonu" (Gmail) card.
8. **Gmail integration** (needs real `GoogleOAuth` credentials — see
   below; with placeholders, "Gmail Bağla" should show a clear
   validation error instead of crashing): connect an account, wait for
   (or trigger) a sync, then check `/settings/email-suggestions` for
   pending suggestions and try Confirm/Dismiss.

For API-only smoke testing without the frontend, see the `curl` examples
under "Trying the API" above, plus `GET /health` and (Development only)
`/openapi/v1.json`.

## Configuration

The app reads `ConnectionStrings:Postgres` and `ConnectionStrings:Redis` from
the standard ASP.NET Core configuration chain — no connection string is
hard-coded anywhere. Locally, set them via `dotnet user-secrets` (see above).
In the container environment, they're supplied as `ConnectionStrings__Postgres`
/ `ConnectionStrings__Redis` environment variables in `docker-compose.yml`.
If unset, the API fails fast on startup with an explanatory error instead of
silently connecting to the wrong database.

`GoogleOAuth:ClientId`/`ClientSecret`/`RedirectUri` (Gmail integration, see
below) are **not** fail-fast — the app runs fine with the placeholder values
in `appsettings.json`; only the `/api/email-integrations/gmail/connect`
endpoint returns a clear error until real credentials are supplied.

`CompanyIntelligence:Enabled` defaults to `false` — the aggregation pipeline
(cross-user company-level Response/Ghosting/Interview/Offer Rate, gated by a
sample-size confidence bucket) is implemented and integration-tested, but
kept switched off until enough real usage exists to make it meaningful; see
`DECISIONS.md`'s Sprint 10 entry. While disabled, every
`/api/company-intelligence/*` endpoint returns `404` for all callers.

## Gmail Integration Setup

Phase 9 (post-MVP) lets a user connect their Gmail account (read-only) so
AfterApply can suggest status updates from hiring-related emails — see
`afterapply-intelligence-platform-plan.md` §10 and `DECISIONS.md`'s Phase 9
entry for the design. The code ships with obvious placeholder OAuth
credentials (`GoogleOAuth:ClientId`/`ClientSecret` in `appsettings.json`) —
the feature is structurally complete but inert until you supply real ones
from your own Google Cloud project:

1. In [Google Cloud Console](https://console.cloud.google.com/), create or
   select a project, then enable the **Gmail API** under APIs & Services.
2. Configure the **OAuth consent screen**: User type **External** is fine for
   personal/private-beta use. Keep the publishing status as **Testing** —
   this avoids Google's manual verification review, which sensitive scopes
   like `gmail.readonly` would otherwise require for a "Production" app.
   Testing mode is capped at a small, fixed list of test users, so add your
   own Google account (and anyone else who'll use this instance) explicitly
   under "Test users" on the consent screen.
3. Add the required scope on the consent screen's scopes list:
   `https://www.googleapis.com/auth/gmail.readonly`.
4. Create an **OAuth 2.0 Client ID** (application type: **Web application**).
   Add your `GoogleOAuth:RedirectUri` value (default:
   `http://localhost:5151/api/email-integrations/gmail/callback`) as an
   **Authorized redirect URI** — it must match byte-for-byte.
5. Set the real values locally via user-secrets (never commit real
   credentials into `appsettings.json`):
   ```bash
   dotnet user-secrets set "GoogleOAuth:ClientId" "<your client id>" --project src/AfterApply.Api
   dotnet user-secrets set "GoogleOAuth:ClientSecret" "<your client secret>" --project src/AfterApply.Api
   ```
   For the container/prod profile, use the `GOOGLE_OAUTH_CLIENT_ID`/
   `GOOGLE_OAUTH_CLIENT_SECRET`/`GOOGLE_OAUTH_REDIRECT_URI` variables in
   `.env.prod` (see `DEPLOYMENT.md`).

No automated test exercises the real Gmail API (see `DECISIONS.md` — the
sync/classification/matching logic is tested against a fake `IGmailClient`
instead); once real credentials are in place, connect a Gmail account from
`/settings` in the frontend as a manual smoke test.

## AI Job Matching Setup

Sprint 8 lets a user paste their CV in `/settings` and get an AI-scored
match (Score/Strong Matches/Missing/Recommendation) against a job
description pasted on an application's detail page — see
`afterapply-intelligence-platform-plan.md` §12 and `DECISIONS.md`'s Sprint 8
entry for the design. The code ships with an obvious placeholder API key
(`OpenAI:ApiKey` in `appsettings.json`) — the feature is structurally
complete but inert until you supply a real one:

1. Create an API key at [platform.openai.com](https://platform.openai.com/api-keys).
2. Set it locally via user-secrets (never commit a real key into
   `appsettings.json`):
   ```bash
   dotnet user-secrets set "OpenAI:ApiKey" "<your api key>" --project src/AfterApply.Api
   ```
   For the container/prod profile, use an `OPENAI_API_KEY` environment
   variable (see `DEPLOYMENT.md`).

No automated test calls the real OpenAI API (see `DECISIONS.md` — the
persist/cache logic is tested against a fake `IJobMatchingProvider`
instead); once a real key is in place, set a CV in `/settings` and compute
a match from an application's detail page as a manual smoke test.

## Browser Extension Setup

Sprint 9 ships a Manifest V3 Chrome/Edge extension (`extension/`) that turns a LinkedIn job
posting page into a tracked AfterApply application with one click — see
`afterapply-intelligence-platform-plan.md` §11 and `DECISIONS.md`'s Sprint 9 entry for the design.
It authenticates with a Personal Access Token generated from `/settings` → Browser Extension,
not the web app's JWT session. Full setup/load-unpacked instructions and known limitations
(LinkedIn scraping selectors are best-effort) are in `extension/README.md`.
