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

## Sprint 13: real cloud deployment (Vercel + Cloud Run + Neon + Upstash)

> **Caveat:** the `gcloud`/IAM commands below were not run against a real
> GCP project in this session (no account exists yet, no `gcloud` CLI
> installed locally) — they follow Google's documented patterns for
> Workload Identity Federation + Cloud Run + Secret Manager, but treat
> them as a well-founded starting point, not a verified transcript.
> Cross-check against `gcloud <command> --help` if something errors.

### 1. Create the free-tier accounts

- **Neon** (neon.tech) — new project, note the pooled connection string
  (`postgresql://user:pass@host/db?sslmode=require`). This becomes
  `ConnectionStrings__Postgres` — EF Core/Npgsql accept Neon's
  connection string format directly (add `Ssl Mode=Require;Trust Server
  Certificate=true` if Npgsql needs it explicit).
- **Upstash** (upstash.com) — new Redis database, note the Redis
  connection string (`rediss://default:pass@host:port`, TLS). This
  becomes `ConnectionStrings__Redis`.
- **Google Cloud** (console.cloud.google.com) — new project, note the
  **Project ID** and **Project Number**. Enable billing (Cloud Run's
  free tier still requires a billing account attached, even though
  usage within the free quota isn't charged).
- **Vercel** (vercel.com) — sign up, connect your GitHub account (no
  project yet, done in step 4).
- **Sentry** (sentry.io) — new organization, two projects: one .NET
  (backend), one Next.js (frontend). Note each DSN, plus the org slug
  and an auth token (Settings → Auth Tokens, `project:releases` scope)
  if you want source-map upload from CI/Vercel.

### 2. One-time GCP setup

```bash
PROJECT_ID="<your-project-id>"
PROJECT_NUMBER="<your-project-number>"
REGION="us-central1"          # pick a Cloud Run free-tier-eligible region
GH_OWNER="<your-github-username-or-org>"
GH_REPO="AfterApply"

gcloud config set project "$PROJECT_ID"
gcloud services enable run.googleapis.com artifactregistry.googleapis.com \
  iamcredentials.googleapis.com secretmanager.googleapis.com

# Artifact Registry — where built API images are pushed
gcloud artifacts repositories create afterapply \
  --repository-format=docker --location="$REGION"

# Deploy service account — used by GitHub Actions to build/push/deploy
gcloud iam service-accounts create afterapply-deployer \
  --display-name="AfterApply CI/CD deployer"

for role in roles/run.admin roles/artifactregistry.writer roles/iam.serviceAccountUser; do
  gcloud projects add-iam-policy-binding "$PROJECT_ID" \
    --member="serviceAccount:afterapply-deployer@${PROJECT_ID}.iam.gserviceaccount.com" \
    --role="$role"
done

# Workload Identity Federation — GitHub Actions authenticates as the
# service account above without a stored long-lived JSON key.
gcloud iam workload-identity-pools create "github-pool" \
  --location="global" --display-name="GitHub Actions"

gcloud iam workload-identity-pools providers create-oidc "github-provider" \
  --location="global" --workload-identity-pool="github-pool" \
  --display-name="GitHub provider" \
  --attribute-mapping="google.subject=assertion.sub,attribute.repository=assertion.repository" \
  --attribute-condition="assertion.repository=='${GH_OWNER}/${GH_REPO}'" \
  --issuer-uri="https://token.actions.githubusercontent.com"

gcloud iam service-accounts add-iam-policy-binding \
  "afterapply-deployer@${PROJECT_ID}.iam.gserviceaccount.com" \
  --role="roles/iam.workloadIdentityUser" \
  --member="principalSet://iam.googleapis.com/projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/github-pool/attribute.repository/${GH_OWNER}/${GH_REPO}"
```

### 3. Secret Manager — one secret per env var the API needs

```bash
printf '%s' "Host=<neon-host>;Port=5432;Database=<db>;Username=<user>;Password=<pass>;Ssl Mode=Require;Trust Server Certificate=true" \
  | gcloud secrets create afterapply-postgres-connection --data-file=-
printf '%s' "<upstash-redis-connection-string>" | gcloud secrets create afterapply-redis-connection --data-file=-
openssl rand -base64 48 | gcloud secrets create afterapply-jwt-signing-key --data-file=-
printf '%s' "<backend-sentry-dsn>" | gcloud secrets create afterapply-sentry-dsn --data-file=-
printf '%s' "<openai-api-key>" | gcloud secrets create afterapply-openai-api-key --data-file=-
printf '%s' "REPLACE_WITH_GOOGLE_OAUTH_CLIENT_ID" | gcloud secrets create afterapply-google-oauth-client-id --data-file=-
printf '%s' "REPLACE_WITH_GOOGLE_OAUTH_CLIENT_SECRET" | gcloud secrets create afterapply-google-oauth-client-secret --data-file=-
printf '%s' "https://<cloud-run-url>/api/email-integrations/gmail/callback" | gcloud secrets create afterapply-google-oauth-redirect-uri --data-file=-
printf '%s' "https://<vercel-domain>" | gcloud secrets create afterapply-web-origin --data-file=-

# Cloud Run's runtime service account (the default compute SA, unless you
# assign a custom one) needs read access to each secret:
RUNTIME_SA="${PROJECT_NUMBER}-compute@developer.gserviceaccount.com"
for s in afterapply-postgres-connection afterapply-redis-connection \
         afterapply-jwt-signing-key afterapply-sentry-dsn afterapply-openai-api-key \
         afterapply-google-oauth-client-id afterapply-google-oauth-client-secret \
         afterapply-google-oauth-redirect-uri afterapply-web-origin; do
  gcloud secrets add-iam-policy-binding "$s" \
    --member="serviceAccount:${RUNTIME_SA}" --role="roles/secretmanager.secretAccessor"
done
```

Placeholders left as `REPLACE_WITH_...` (Gmail OAuth) are fine — matches
the existing "stays inert until configured" pattern (README "Gmail
Integration Setup"); fill them in only when you actually set up that
Google Cloud OAuth client.

### 4. GitHub repo secrets and first deploy

In GitHub → repo Settings → Secrets and variables → Actions, add:

- `GCP_PROJECT_ID`, `GCP_REGION` (same `$REGION` as above)
- `GCP_WORKLOAD_IDENTITY_PROVIDER` — full resource name:
  `projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/github-pool/providers/github-provider`
- `GCP_SERVICE_ACCOUNT` — `afterapply-deployer@${PROJECT_ID}.iam.gserviceaccount.com`

Then run the deploy workflow once manually (`.github/workflows/deploy-backend.yml`
is `workflow_dispatch`-only on purpose, see the file's own comment):

```bash
gh workflow run deploy-backend.yml
```

Apply migrations against Neon (same command as the local prod profile,
step 3 above, just pointed at the Neon connection string instead of
`localhost:5434`). Once the Cloud Run service URL is known, map a custom
domain for free automatic SSL:

```bash
gcloud run domain-mappings create --service=afterapply-api \
  --domain=api.yourdomain.com --region="$REGION"
```

### 5. Vercel (frontend)

1. Vercel dashboard → New Project → import the GitHub repo → set **Root
   Directory** to `web` (Vercel auto-detects Next.js, no Dockerfile
   needed — `web/Dockerfile` stays relevant only for the local
   docker-compose profile above, not the real Vercel deploy).
2. Project → Settings → Environment Variables:
   `NEXT_PUBLIC_API_BASE_URL` = the Cloud Run service URL (or
   `https://api.yourdomain.com` once step 4's domain mapping is live),
   `NEXT_PUBLIC_SENTRY_DSN` = the frontend Sentry DSN, and optionally
   `SENTRY_ORG`/`SENTRY_PROJECT`/`SENTRY_AUTH_TOKEN` for readable
   (unminified) stack traces in Sentry.
3. Vercel deploys automatically on every push to `main` (its own GitHub
   integration — no custom GitHub Actions workflow needed for the
   frontend) and provides a free custom-domain SSL certificate.
4. Once the Vercel domain is final, update the
   `afterapply-web-origin` secret (step 3) to match, and redeploy the
   backend so CORS allows it.

### 6. Switching CI from manual to automatic

Once the above is verified working end-to-end once, uncomment the
`push: branches: [main]` trigger in `deploy-backend.yml` (currently
commented out on purpose) to deploy automatically going forward.
