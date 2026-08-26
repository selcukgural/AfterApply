# Deployment

Sprint 7 scope: a locally-verifiable "prod-like" Docker Compose profile,
**not** a real cloud deployment. Cloud provider is now decided (see
`DECISIONS.md`, "5. Cloud provider — DECIDED": everything on Google
Cloud — Cloud Run × 2, Cloud SQL, Memorystore) but not yet wired up —
that's Sprint 13 work. This document remains the stand-in the Sprint 7
DoD requires ("prod-benzeri bir ortamda doğrulanabilir") until Sprint 13
replaces it with real deployment instructions.

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
  instances (Cloud SQL / Memorystore, per the Sprint 13 decision) instead.
- **No secrets manager.** `.env.prod` is a plain file; a real deployment
  should use the target cloud's secrets manager instead.
- **Cloud provider decided, not yet wired up** — everything on Google
  Cloud: Cloud Run × 2 (`api` + `web`), Cloud SQL (Postgres), Memorystore
  (Redis), see `DECISIONS.md` §5. The container images built here
  (`src/AfterApply.Api/Dockerfile`, `web/Dockerfile`) are the deployable
  artifacts either way — no further image changes should be needed,
  only the hosting/networking/secrets layer around them (Sprint 13).

## Sprint 13: real cloud deployment (all on Google Cloud)

> **Caveat:** the `gcloud`/IAM commands below were not run against a real
> GCP project in this session (no `gcloud` CLI installed locally) — they
> follow Google's currently-documented patterns for Workload Identity
> Federation, Cloud Run ↔ Cloud SQL, Cloud Run ↔ Memorystore, and Secret
> Manager (re-verified against Google's docs when this section was
> rewritten for the all-GCP architecture), but treat them as a
> well-founded starting point, not a verified transcript. Cross-check
> against `gcloud <command> --help` if something errors — a couple of
> specific spots are flagged below where the exact flag value wasn't
> independently confirmed.

> **Cost note (see `DECISIONS.md` §5):** Cloud Run stays free forever.
> Cloud SQL and Memorystore do **not** — they're free only for the
> 90-day/$300 GCP trial. Budget roughly $10-15/mo (Cloud SQL) + $35-40/mo
> (Memorystore) once that trial ends, unless you downsize/delete before
> then.

### 1. Accounts

Just **Google Cloud** (console.cloud.google.com) and **Sentry**
(sentry.io, unchanged from before — error tracking wasn't folded into
GCP). No Neon, Upstash, or Vercel accounts needed anymore.

- **Google Cloud**: new project, note the **Project ID** and **Project
  Number**. Enable billing (required even for trial-credit usage).
- **Sentry**: new organization, two projects — one .NET (backend), one
  Next.js (frontend). Note each DSN, plus the org slug and (optionally,
  for readable stack traces) an auth token (Settings → Auth Tokens,
  `project:releases` scope).

### 2. One-time GCP setup

```bash
PROJECT_ID="<your-project-id>"
PROJECT_NUMBER="<your-project-number>"
REGION="us-central1"          # pick one region, used for every resource below
GH_OWNER="<your-github-username-or-org>"
GH_REPO="AfterApply"

gcloud config set project "$PROJECT_ID"
gcloud services enable run.googleapis.com artifactregistry.googleapis.com \
  iamcredentials.googleapis.com secretmanager.googleapis.com \
  sqladmin.googleapis.com redis.googleapis.com

# Artifact Registry — where built API/web images are pushed
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

# --- Cloud SQL for PostgreSQL ---
# db-f1-micro is the cheapest shared-core tier; if gcloud rejects
# --edition=ENTERPRISE for it, try db-g1-small instead (unverified which
# is currently offered — check `gcloud sql tiers list`).
gcloud sql instances create afterapply-db \
  --database-version=POSTGRES_16 --tier=db-f1-micro --region="$REGION" \
  --storage-size=10 --storage-auto-increase --edition=ENTERPRISE
gcloud sql databases create afterapply --instance=afterapply-db
DB_PASSWORD="$(openssl rand -base64 24)"
gcloud sql users create afterapply --instance=afterapply-db --password="$DB_PASSWORD"
echo "DB_PASSWORD=$DB_PASSWORD"   # you'll need this once, for the secret below — don't lose it

# --- Memorystore for Redis ---
# --size is in GiB; 1 is the intended minimum but the exact floor wasn't
# independently confirmed (Google's own quickstart example uses 2) — if
# gcloud rejects 1, use 2. --network=default uses the project's existing
# default VPC (already large enough) — no custom VPC was created.
gcloud redis instances create afterapply-redis \
  --size=1 --region="$REGION" --tier=basic --network=default

# The runtime service account (the one Cloud Run services actually run
# as, not the deployer above) needs to read Postgres over the Cloud SQL
# connector:
RUNTIME_SA="${PROJECT_NUMBER}-compute@developer.gserviceaccount.com"
gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:${RUNTIME_SA}" --role="roles/cloudsql.client"
```

### 3. Secret Manager

```bash
# Cloud SQL — Unix socket path, not a host:port. Npgsql/PostgreSQL
# appends the .s.PGSQL.5432 suffix itself; SSL Mode=Disable is correct
# here (not a downgrade) — the socket connection is already encrypted by
# Cloud Run's built-in Cloud SQL connector.
printf '%s' "Host=/cloudsql/${PROJECT_ID}:${REGION}:afterapply-db;Database=afterapply;Username=afterapply;Password=${DB_PASSWORD};SSL Mode=Disable" \
  | gcloud secrets create afterapply-postgres-connection --data-file=-

# Memorystore — private IP, no TLS needed (already inside the private VPC).
REDIS_IP="$(gcloud redis instances describe afterapply-redis --region="$REGION" --format='value(host)')"
printf '%s' "${REDIS_IP}:6379" | gcloud secrets create afterapply-redis-connection --data-file=-

openssl rand -base64 48 | gcloud secrets create afterapply-jwt-signing-key --data-file=-
printf '%s' "<backend-sentry-dsn>" | gcloud secrets create afterapply-sentry-dsn --data-file=-
printf '%s' "<openai-api-key-veya-REPLACE_WITH_OPENAI_API_KEY>" | gcloud secrets create afterapply-openai-api-key --data-file=-
printf '%s' "REPLACE_WITH_GOOGLE_OAUTH_CLIENT_ID" | gcloud secrets create afterapply-google-oauth-client-id --data-file=-
printf '%s' "REPLACE_WITH_GOOGLE_OAUTH_CLIENT_SECRET" | gcloud secrets create afterapply-google-oauth-client-secret --data-file=-
printf '%s' "https://REPLACE-ONCE-DEPLOYED/api/email-integrations/gmail/callback" | gcloud secrets create afterapply-google-oauth-redirect-uri --data-file=-
# Placeholder — the real web Cloud Run URL isn't known until step 4's
# deploy-web run; step 4 shows how to update this in place afterward.
printf '%s' "https://REPLACE-ONCE-DEPLOYED" | gcloud secrets create afterapply-web-origin --data-file=-

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
- `SENTRY_DSN_WEB` — the frontend Sentry DSN (not sensitive, it's meant
  to ship in the browser bundle, but stored as a secret for consistency)
- Optional, only if you want readable stack traces in Sentry:
  `SENTRY_ORG`, `SENTRY_PROJECT_WEB`, `SENTRY_AUTH_TOKEN`

`.github/workflows/deploy.yml` has two independent jobs,
`deploy-backend` and `deploy-web` (both `workflow_dispatch`-only on
purpose — see the file's own comment). Run them **in this order**, since
`deploy-web`'s build needs `deploy-backend`'s URL baked in, and closing
the CORS loop needs `deploy-web`'s URL in turn:

GitHub Actions doesn't support running a single job out of a
`workflow_dispatch`-triggered workflow — running `deploy.yml` as-is
triggers both jobs together, but `deploy-web` will fail the first time
(`GCP_API_URL` doesn't exist yet). That's fine, it's a one-time
bootstrap wrinkle:

```bash
# 1. First run: comment out the `deploy-web:` job in deploy.yml (or just
#    let it fail — deploy-backend still succeeds independently), then:
gh workflow run deploy.yml
gcloud run services describe afterapply-api --region="$REGION" --format='value(status.url)'
# → add this URL as the GCP_API_URL GitHub secret; restore deploy-web if you commented it out.

# 2. Second run: now both jobs succeed. Note the web URL:
gh workflow run deploy.yml
gcloud run services describe afterapply-web --region="$REGION" --format='value(status.url)'

# 3. Close the CORS loop with the real web URL, then redeploy the API.
printf '%s' "https://<the-web-url-from-step-2>" \
  | gcloud secrets versions add afterapply-web-origin --data-file=-
gcloud run services update afterapply-api --region="$REGION" \
  --update-secrets=Cors__AllowedOrigins__0=afterapply-web-origin:latest
```

This is only a one-time bootstrap cost — every deploy after this, both
jobs already have what they need.

Once both services are up, map custom domains for free automatic SSL if
you have a domain (optional, separate checklist item — DEVELOPMENT_PLAN.md
Sprint 13):

```bash
gcloud run domain-mappings create --service=afterapply-api \
  --domain=api.yourdomain.com --region="$REGION"
gcloud run domain-mappings create --service=afterapply-web \
  --domain=yourdomain.com --region="$REGION"
```

### 5. Migrations

Cloud SQL isn't reachable by Unix socket from a local machine the way
Cloud Run reaches it. Two options — the **Cloud SQL Auth Proxy** is
recommended (no instance configuration change, closer to what CI would
do too):

```bash
# Install once: https://cloud.google.com/sql/docs/postgres/sql-proxy#install
cloud-sql-proxy "${PROJECT_ID}:${REGION}:afterapply-db" &
dotnet ef database update \
  --project src/AfterApply.Infrastructure --startup-project src/AfterApply.Api \
  --connection "Host=127.0.0.1;Port=5432;Database=afterapply;Username=afterapply;Password=<DB_PASSWORD from step 2>"
```

(Alternative, no proxy: temporarily
`gcloud sql instances patch afterapply-db --authorized-networks=<your-public-ip>/32`,
connect directly, then remove the authorized network again — more moving
parts, not recommended as the default path.)

### 6. Switching CI from manual to automatic

Once the above is verified working end-to-end once, uncomment the
`push: branches: [main]` trigger in `deploy.yml` (currently commented
out on purpose) to deploy automatically going forward.
