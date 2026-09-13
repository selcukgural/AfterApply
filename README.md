# e-kariyerim

Job Application Tracker + Personal Analytics. See `ekariyerim-intelligence-platform-plan.md`
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
- For the container path: Podman (or Docker) + `docker-compose` on PATH.
  This repo was verified against Podman — no Docker Desktop required.

## Quick start — native local Postgres (recommended for day-to-day dev)

> ⚠️ **Port conflicts:** if you run other local projects with their own
> Postgres containers, the default port (5432) may already be taken by
> something else. Check with `lsof -nP -iTCP:5432 -sTCP:LISTEN` before
> assuming it is free, and adjust the connection string below accordingly.

1. Start Postgres (e.g. `brew services start postgresql@17`).
2. `createdb afterapply_dev`
3. `dotnet user-secrets init --project src/AfterApply.Api` (one-time)
4. ```bash
   dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=afterapply_dev;Username=$(whoami)" --project src/AfterApply.Api
   dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/AfterApply.Api
   ```
   (adjust the port if you hit a conflict per the warning above)
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

Uses an offset host port (**5434** for Postgres) to reduce the odds of
colliding with a native Postgres or another local project's containers —
check for conflicts on your machine and adjust `docker-compose.yml` if
needed.

1. `cp .env.example .env` and fill in `POSTGRES_PASSWORD`
2. `podman compose up --build` (or `docker compose up --build`)
3. `curl -i http://localhost:8080/health`
4. `podman compose down` (add `-v` to also drop the postgres volume)

`docker-compose.yml` (dev) only runs Postgres/API — it does **not**
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
  On this machine the suite is **far** faster with collection parallelism off —
  242 tests in ~3 minutes, against a parallel run that reached ~90 of the test
  classes in 45 and had to be killed. Every test class stands up its own host
  and database, so running many at once thrashes rather than overlaps:
  ```bash
  dotnet test tests/AfterApply.IntegrationTests -- xunit.parallelizeTestCollections=false
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
  **No test talks to the internet.** Every host the integration suite builds has
  its outbound HTTP blocked and starts no Hangfire background server unless the
  test asks for one — the two things that made this suite hang, crash and, once,
  open real GitHub issues. If a new test needs an HTTP response, stub that client
  with `.ConfigurePrimaryHttpMessageHandler(...)`; if it asserts what a background
  job did, add `UseSetting("Hangfire:ServerEnabled", "true")` to its own factory.
  `NoOutboundHttpTests` and `HangfireServerInTestsTests` guard both rules, and
  DECISIONS.md (2026-09-09) has the reasoning. Current shape on this machine:
  298 tests, green (~3.5 min on an idle machine, longer under load).
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
   consent checkbox. `/cookies` is its companion: the cookie disclosure
   KVKK asks for. There is deliberately no consent banner — every cookie
   the app sets is strictly necessary or functional, so there is nothing
   optional to consent to (see `DECISIONS.md`, 2026-09-07). If you add an
   analytics or advertising cookie, `browserStorage.test.ts` fails until
   both the policy and a consent flow catch up.
7. **Settings**: `/settings` (requires login) — account data export
   (download), account deletion (asks for password — not for an account
   created with Google, which has none), and the "Mail
   Forwarding" card (behind `EmailForwarding:Enabled`, off by default).
8. **Sign in with Google** (only once `GoogleAuth:ClientId`/`ClientSecret`
   are set, see "Google Sign-In Setup" below): the "Continue with Google"
   button on `/login` and `/register` — a Google account new to the app
   lands on a "complete your sign-up" step (name + privacy consent) before
   the account exists; one whose verified email matches an existing account
   is linked to it and signed straight in.

For API-only smoke testing without the frontend, see the `curl` examples
under "Trying the API" above, plus `GET /health` and (Development only)
`/openapi/v1.json`.

## Configuration

The app reads `ConnectionStrings:Postgres` from the standard ASP.NET Core
configuration chain — no connection string is hard-coded anywhere. Locally,
set it via `dotnet user-secrets` (see above). In the container environment,
it's supplied as the `ConnectionStrings__Postgres` environment variable in
`docker-compose.yml`. If unset, the API fails fast on startup with an
explanatory error instead of silently connecting to the wrong database.

Caching is in-process only (`HybridCache` with no distributed backend). The
Redis/Memorystore L2 that used to sit behind it was removed — see
`DECISIONS.md` "Redis kaldırıldı" for why it never changed an outcome. There is
no cache connection string to configure, and nothing to run locally for it.

`CompanyIntelligence:Enabled` defaults to `false` — the aggregation pipeline
(cross-user company-level Response/Ghosting/Interview/Offer Rate, gated by a
sample-size confidence bucket) is implemented and integration-tested, but
kept switched off until enough real usage exists to make it meaningful; see
`DECISIONS.md`'s Sprint 10 entry. While disabled, every
`/api/company-intelligence/*` endpoint returns `404` for all callers.

`CompanyReviews` (company reviews — the public `/companies` pages): `Enabled` (default `true`;
off → every review endpoint answers 404 and the web app hides the pages, so the feature can ship
dark), `MaxReviewsPerUser` (10; an admin can override it per account through
`PUT /api/admin/users/{id}/review-quota`), `MinimumReviewsForScore` (3 — below it a company shows
no score), `PriorWeight` (5 — the `m` in the Bayesian average, documented on `/companies/scoring`).
Rate limits: `RateLimiting:CompanyReviewWrite|CompanyReviewReport|CompanyReviewHelpful|CompanyPublicSearch`.

## CV storage (`/cv`)

Users can keep up to 10 CV files (PDF/DOC/DOCX, 5 MB each), download them,
delete them, mark one as the default, and record which CV an application was
sent with. Files live in object storage; Postgres only holds the metadata.

Uploading requires **explicit consent, taken per upload** — a CV can carry
special-category personal data (KVKK art. 6). The checkbox is never
pre-ticked and clears again after each upload, the file picker and drop zone
stay disabled until it is ticked, and the server refuses an upload without it
before reading the file (`CV_CONSENT_REQUIRED`). The moment consent was given
is stored on the row (`CvDocuments.ConsentAcceptedAt`); it is null on rows
written before 2026-09-07, which is deliberate — backfilling it would record a
consent nobody gave.

Which storage backend is used comes from the `Storage` configuration section:

| Setting | Local default | Production |
|---|---|---|
| `Storage:Provider` | `FileSystem` | `GoogleCloudStorage` |
| `Storage:BucketName` | — | `afterapply-cvs` (set by `deploy.yml`) |
| `Storage:LocalRootPath` | a directory under the OS temp dir | — |
| `Storage:MaxFileSizeBytes` | `5242880` (5 MB) | same |

So a fresh clone needs no configuration at all: uploads land in a temp
directory. Startup **refuses** `FileSystem` when
`ASPNETCORE_ENVIRONMENT=Production`, because Cloud Run's filesystem is
in-memory and per-instance — an upload written there would vanish on the next
revision. See `DEPLOYMENT.md` §11 for creating the bucket (and for why its
soft-delete window is deliberately turned off), and `DECISIONS.md` 2026-09-07
for why downloads are proxied through the API instead of served from signed
URLs.

The first-page preview on `/cv` is rendered in the browser with `pdf.js`;
nothing about a CV is ever read, converted or analysed server-side, and no
part of it is sent to any third party.

## AI Job Matching Setup

Sprint 8 lets a user paste their CV in `/settings` and get an AI-scored
match (Score/Strong Matches/Missing/Recommendation) against a job
description pasted on an application's detail page — see
`ekariyerim-intelligence-platform-plan.md` §12 and `DECISIONS.md`'s Sprint 8
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

## Google Sign-In Setup

"Continue with Google" on the login/register pages is an authorization-code + PKCE flow driven by
a plain redirect to `accounts.google.com` (no Google script on the page, so the web app's CSP is
untouched). The API exchanges the code server-side (`GoogleAuthClient`) and only reads the ID token —
scopes are `openid email profile`, nothing that needs Google's app verification. Like OpenAI/Resend,
the feature is **inert until configured**: with `GoogleAuth:ClientId`/`ClientSecret` unset,
`GET /api/config` reports it disabled, the button is not rendered and `/api/auth/google*` answer 404.

1. In Google Cloud Console → APIs & Services → Credentials → Create credentials → **OAuth client ID**,
   type **Web application**. Authorized redirect URIs must contain, for every environment the client
   serves (exact match, one per locale):
   - `http://localhost:3000/tr/auth/google/callback` and `http://localhost:3000/en/auth/google/callback`
   - `<WEB_ORIGIN>/tr/auth/google/callback` and `<WEB_ORIGIN>/en/auth/google/callback` for prod
   The OAuth consent screen needs only the non-sensitive `openid`/`email`/`profile` scopes; publish it
   ("In production") before real users can sign in, otherwise only the test users listed there can.
2. Set the two values locally via user-secrets:
   ```bash
   dotnet user-secrets set "GoogleAuth:ClientId" "<client id>.apps.googleusercontent.com" --project src/AfterApply.Api
   dotnet user-secrets set "GoogleAuth:ClientSecret" "<client secret>" --project src/AfterApply.Api
   ```
   For the container/prod profile use `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` (see
   `.env.prod.example` and `DEPLOYMENT.md`).

`App:WebBaseUrl` must be the origin the redirect URI is under — the API refuses a redirect URI on any
other origin before ever calling Google. No automated test calls Google; `GoogleSignInTests` runs the
whole flow against a fake `IGoogleAuthClient`, and `GoogleIdTokenReaderTests`/`GoogleSignupTokenTests`
pin the token checks.

## LinkedIn Sign-In Setup

"Continue with LinkedIn" works the same way as Google's — an authorization-code flow driven by a
plain redirect to `linkedin.com`, exchanged server-side (`LinkedInAuthClient`), inert until
configured. Two differences from Google, both deliberate: no PKCE (LinkedIn's OAuth implementation
doesn't call for a `code_verifier` from a confidential, client-secret-holding client), and the ID
token's signature IS fully verified against LinkedIn's published JWKS (`LinkedInJwksProvider`/
`LinkedInIdTokenReader`) rather than trusted on the TLS channel alone — LinkedIn's OpenID Connect
response also makes `email`/`email_verified` optional, unlike Google's, so a LinkedIn sign-up can
require the user to type an email by hand when LinkedIn didn't supply one.

1. In the [LinkedIn Developer Portal](https://www.linkedin.com/developers/apps/new), create an app
   tied to a LinkedIn Page you administer, then add the **"Sign In with LinkedIn using OpenID
   Connect"** product under the Products tab — self-serve, no review wait. Under Auth → OAuth 2.0
   settings, add every redirect URL the client serves (exact match, one per locale):
   - `<WEB_ORIGIN>/tr/auth/linkedin/callback` and `<WEB_ORIGIN>/en/auth/linkedin/callback`
   LinkedIn requires these to be `https://` — unlike Google, it does not accept a plain
   `http://localhost` redirect, so local testing needs a tunnel (e.g. ngrok) or a staging domain.
2. Set the two values locally via user-secrets:
   ```bash
   dotnet user-secrets set "LinkedInAuth:ClientId" "<client id>" --project src/AfterApply.Api
   dotnet user-secrets set "LinkedInAuth:ClientSecret" "<client secret>" --project src/AfterApply.Api
   ```
   For the container/prod profile use `LINKEDIN_CLIENT_ID` / `LINKEDIN_CLIENT_SECRET` (see
   `.env.prod.example` and `DEPLOYMENT.md`).

Same `App:WebBaseUrl` origin check as Google. No automated test calls LinkedIn; `LinkedInSignInTests`
runs the whole flow against a fake `ILinkedInAuthClient`, and
`LinkedInIdTokenReaderTests`/`LinkedInJwksProviderTests`/`LinkedInSignupTokenTests` pin the signature
and token checks.

## GitHub Sign-In Setup

"Continue with GitHub" is the third provider and the simplest of the three: a plain OAuth
authorization-code flow, exchanged server-side (`GitHubAuthClient`), inert until configured. What
makes it different from the other two is that **there is no OpenID Connect here** — GitHub's OAuth
Apps issue no `id_token`, so there is no signed assertion to verify and no JWKS to fetch. The
identity is read over TLS from `api.github.com` with two calls (`GET /user`, `GET /user/emails`) and
the access token is discarded the moment they return; it is never stored, logged or sent to the
client. No PKCE either (GitHub's OAuth App endpoints don't take one), so the browser's single-use
`state` is the login-CSRF defence, exactly as in the LinkedIn flow.

Two consequences worth knowing before you wire it up, both handled by
`GitHubProfileReader`:

- **The email is optional.** A GitHub account can keep every address private, and an app can be
  granted without the `user:email` scope. Only a GitHub-*verified*, deliverable address is ever used
  to match an existing e-kariyerim account; `@users.noreply.github.com` addresses are dropped
  (verified, but nothing sent to them arrives, so a password reset would silently vanish). When
  nothing qualifies the user types an email on the sign-up form — the same path LinkedIn already
  had.
- **The name is one free-text field**, not a given/family pair, so it is split on the last space and
  both halves land in editable inputs.

1. Create a **GitHub OAuth App** at
   [Settings → Developer settings → OAuth Apps](https://github.com/settings/developers) — *not* a
   GitHub App, which is the installable-on-repositories kind and is not what a login needs. Add
   every callback URL the client serves (GitHub matches them exactly by default, and an OAuth App
   accepts more than one):
   - `<WEB_ORIGIN>/tr/auth/github/callback` and `<WEB_ORIGIN>/en/auth/github/callback`

   Unlike LinkedIn, this one **can** be exercised locally: GitHub follows the OAuth RFC's advice
   against the name `localhost`, so register the loopback form (`http://127.0.0.1:3000/tr/auth/github/callback`)
   and open the web app on `http://127.0.0.1:3000` with `App:WebBaseUrl` set to match.
2. Set the two values locally via user-secrets:
   ```bash
   dotnet user-secrets set "GitHubAuth:ClientId" "<client id>" --project src/AfterApply.Api
   dotnet user-secrets set "GitHubAuth:ClientSecret" "<client secret>" --project src/AfterApply.Api
   ```
   For the container/prod profile use `GITHUB_CLIENT_ID` / `GITHUB_CLIENT_SECRET` (see
   `.env.example`, `.env.prod.example` and `DEPLOYMENT.md`).

The scopes the web app requests are `read:user user:email` — the profile and the address list,
nothing that can reach a repository, public or private. Same `App:WebBaseUrl` origin check as the
other two. No automated test calls GitHub; `GitHubSignInTests` runs the whole flow against a fake
`IGitHubAuthClient`, and `GitHubProfileReaderTests`/`GitHubSignupTokenTests` pin the email-selection
and token rules.

## Browser Extension Setup

Sprint 9 ships a Manifest V3 Chrome/Edge extension (`extension/`) that turns a LinkedIn job
posting page into a tracked e-kariyerim application with one click — see
`ekariyerim-intelligence-platform-plan.md` §11 and `DECISIONS.md`'s Sprint 9 entry for the design.
It authenticates with a Personal Access Token generated from `/settings` → Browser Extension,
not the web app's JWT session. Full setup/load-unpacked instructions and known limitations
(LinkedIn scraping selectors are best-effort) are in `extension/README.md`.
