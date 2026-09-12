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

- `postgres` no longer publishes a host port — the dev file exposes it
  for local `psql` access; nothing outside the Compose network needs it
  in this profile.
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

For the real cloud deployment (Sprint 13+), this "explicit step" is
automated as part of `deploy.yml` rather than run by hand — see "Automatic
migrations in CI" under Sprint 13 below. The invariant above still holds:
nothing calls `Database.Migrate()` on container boot; the automation just
moved the explicit step from a human's terminal into the deploy pipeline.

### Migrations must stay readable by the code that is still running

The migration job finishes before the new API revision takes traffic, so for a
minute or two the **old** image is serving against the **new** schema — and a
Cloud Run rollback puts it back there indefinitely. A migration therefore has to
leave the schema usable by the previous release, not only by the one shipping
with it.

The trap is a new `NOT NULL` column. EF Core writes an explicit column list, so
the old model's `INSERT` simply omits the column and relies on the database
default. `AddColumn` creates that default for you — dropping it in the same
migration turns every insert from the old image into a not-null violation:

    ERROR: null value in column "Origin" violates not-null constraint

Keep the default in the migration that adds the column, and drop it in a later
one once the release that always supplies the value is live (expand, then
contract). The same rule rules out renaming or dropping a column still read by
the running release.

## What's still missing for a real cloud deployment

This profile deliberately stops short of being cloud-ready:

- **No reverse proxy / TLS.** A real deployment needs Caddy/Nginx (or a
  cloud load balancer) in front of `api` and `web`, terminating TLS on a
  real domain. Not built here — there's no domain/cloud target yet to
  configure it against, and building one speculatively would be
  unverifiable dead weight per this project's YAGNI rule (spec §31.20).
- **No managed Postgres.** This profile runs it in a container with a
  local volume — a real deployment should point
  `ConnectionStrings__Postgres` at a managed instance (Cloud SQL, per the
  Sprint 13 decision) instead.
- **No secrets manager.** `.env.prod` is a plain file; a real deployment
  should use the target cloud's secrets manager instead.
- **Cloud provider decided, not yet wired up** — everything on Google
  Cloud: Cloud Run × 2 (`api` + `web`) and Cloud SQL (Postgres), see
  `DECISIONS.md` §5. (Memorystore was part of this until 2026-09-06; see
  "Redis kaldırıldı".) The container images built here
  (`src/AfterApply.Api/Dockerfile`, `web/Dockerfile`) are the deployable
  artifacts either way — no further image changes should be needed,
  only the hosting/networking/secrets layer around them (Sprint 13).

## Sprint 13: real cloud deployment (all on Google Cloud)

> **Verified (2026-08-26):** this section was run end-to-end against a
> real project (`ekariyerim`, `europe-west1`) — API + web live on Cloud
> Run, Cloud SQL connected, custom domain mapped, a real
> user registration round-tripped (201 + JWT). Steps 5-7 below (make
> public, migrate, verify) were added *because* the first attempt skipped
> them and produced a 403 and then a 500 — see DECISIONS.md "Sprint 13 —
> gerçek deploy" for the full list of what broke and why. `db-f1-micro` +
> `--edition=ENTERPRISE` (step 2) was accepted as-is, no fallback needed.
>
> **Updated 2026-09-06:** the Memorystore/Redis instance this section used
> to create was deleted and its steps removed — see `DECISIONS.md` "Redis
> kaldırıldı". An environment built before that date also needs the
> teardown at the end of this section.

> **Cost note (see `DECISIONS.md` §5):** Cloud Run stays free forever.
> Cloud SQL does **not** — it's free only for the 90-day/$300 GCP trial.
> Budget roughly $10-15/mo once that trial ends. Memorystore used to add
> another $35-40/mo on top of that; it was deleted on 2026-09-06 and is
> no longer part of this stack.

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
  sqladmin.googleapis.com

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

openssl rand -base64 48 | gcloud secrets create afterapply-jwt-signing-key --data-file=-
printf '%s' "<backend-sentry-dsn>" | gcloud secrets create afterapply-sentry-dsn --data-file=-
printf '%s' "<openai-api-key-veya-REPLACE_WITH_OPENAI_API_KEY>" | gcloud secrets create afterapply-openai-api-key --data-file=-
# Forgot-password email (Resend, resend.com) — sending domain mail.ekariyerim.com must be
# verified in the Resend dashboard first (SPF/DKIM records added to Cloudflare DNS).
printf '%s' "<resend-api-key-veya-REPLACE_WITH_RESEND_API_KEY>" | gcloud secrets create afterapply-resend-api-key --data-file=-
# Sign in with Google — an OAuth 2.0 client (type "Web application") from this project's
# APIs & Services -> Credentials, with <web-origin>/tr/auth/google/callback and /en/... as
# authorized redirect URIs (see README.md "Google Sign-In Setup"). Both secrets must EXIST for
# deploy.yml's --set-secrets to succeed; leave the values empty and the feature simply stays
# off (button hidden, /api/auth/google* answer 404) until real values are added.
printf '%s' "<google-oauth-client-id-veya-bos>" | gcloud secrets create afterapply-google-client-id --data-file=-
printf '%s' "<google-oauth-client-secret-veya-bos>" | gcloud secrets create afterapply-google-client-secret --data-file=-
# Sign in with LinkedIn — a LinkedIn Developer app (tied to the e-kariyerim LinkedIn Page, with the
# "Sign In with LinkedIn using OpenID Connect" product enabled), with <web-origin>/tr/auth/linkedin/callback
# and /en/... as authorized redirect URLs (HTTPS only; see README.md "LinkedIn Sign-In Setup"). Same
# contract as Google: both secrets must EXIST for --set-secrets, empty values keep the feature off.
printf '%s' "<linkedin-client-id-veya-bos>" | gcloud secrets create afterapply-linkedin-client-id --data-file=-
printf '%s' "<linkedin-client-secret-veya-bos>" | gcloud secrets create afterapply-linkedin-client-secret --data-file=-
# Sign in with GitHub — a GitHub OAuth App (Settings -> Developer settings -> OAuth Apps, NOT a GitHub
# App), with <web-origin>/tr/auth/github/callback and /en/... as callback URLs (an OAuth App takes up
# to 10 of them, matched exactly; see README.md "GitHub Sign-In Setup"). Same contract as the other
# two: both secrets must EXIST for --set-secrets, empty values keep the feature off.
printf '%s' "<github-client-id-veya-bos>" | gcloud secrets create afterapply-github-client-id --data-file=-
printf '%s' "<github-client-secret-veya-bos>" | gcloud secrets create afterapply-github-client-secret --data-file=-
# Placeholder — the real web Cloud Run URL isn't known until step 4's
# deploy-web run; step 4 shows how to update this in place afterward. Also used as
# App:WebBaseUrl (see deploy.yml) — same value, used to build links in outbound email.
printf '%s' "https://REPLACE-ONCE-DEPLOYED" | gcloud secrets create afterapply-web-origin --data-file=-

for s in afterapply-postgres-connection \
         afterapply-jwt-signing-key afterapply-sentry-dsn afterapply-openai-api-key \
         afterapply-resend-api-key \
         afterapply-google-client-id afterapply-google-client-secret \
         afterapply-linkedin-client-id afterapply-linkedin-client-secret \
         afterapply-github-client-id afterapply-github-client-secret \
         afterapply-web-origin; do
  gcloud secrets add-iam-policy-binding "$s" \
    --member="serviceAccount:${RUNTIME_SA}" --role="roles/secretmanager.secretAccessor"
done
```

> **Adding a secret later? Both halves, or the next deploy fails.** `gcloud secrets create` alone
> is not enough — the runtime service account also needs
> `roles/secretmanager.secretAccessor` on it, which is what the loop above does. Skip the binding
> and `gcloud run deploy` will not warn: it fails while creating the revision, with
> `Permission denied on secret: .../versions/latest for Revision service account ...`. Nothing is
> served from the broken revision, so production keeps running on the previous one — the symptom is
> a red deploy and a prod that silently stays one version behind. This happened on 2026-09-09 with
> the two `afterapply-github-*` secrets; the fix is to run the binding for the new names and re-run
> the failed job (`gh run rerun <id> --failed`). So: add the name to the `create` lines **and** to
> the loop, in the same edit.

### 3a. Granting admin access

`/api/admin/metrics` and the `/admin/metrics` page are gated on a single column, `Users.IsAdmin`.
There is deliberately **no secret and no env var** for this: config is read when the process
starts, so changing it would mean waiting for a new Cloud Run instance or forcing one with a
redeploy. Read from the database it takes effect on the very next request.

Connect with the Cloud SQL Auth Proxy (see §9) and grant it by hand:

```sql
-- grant
UPDATE "Users" SET "IsAdmin" = true  WHERE "Email" = 'you@example.com';
-- revoke
UPDATE "Users" SET "IsAdmin" = false WHERE "Email" = 'whoever@example.com';
-- who has it right now
SELECT "Email" FROM "Users" WHERE "IsAdmin";
```

The account has to exist first — sign up in the app, then run the UPDATE. Everyone else gets `403`.
Nobody is an admin until the first UPDATE runs, which is the right state to deploy in.

Note there is no audit trail of who granted it or when. That is an accepted trade while admin means
"can read aggregate counts"; revisit it if the surface ever grows past that.

### 4. GitHub repo secrets and first deploy

In GitHub → repo Settings → Secrets and variables → Actions, add:

- `GCP_PROJECT_ID`, `GCP_REGION` (same `$REGION` as above)
- `GCP_WORKLOAD_IDENTITY_PROVIDER` — full resource name:
  `projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/github-pool/providers/github-provider`
- `GCP_SERVICE_ACCOUNT` — `afterapply-deployer@${PROJECT_ID}.iam.gserviceaccount.com`
- `SENTRY_DSN_WEB` — the frontend Sentry DSN (not sensitive, it's meant
  to ship in the browser bundle, but stored as a secret for consistency)
- `GCP_CV_BUCKET` — the CV bucket's name (`afterapply-cvs`; §11 creates
  it). Not sensitive either, and for the same kind of reason: the bucket
  is protected by `public_access_prevention=enforced` plus IAM, not by
  its name being unguessable — stored as a secret to match
  `GCP_PROJECT_ID`/`GCP_REGION` rather than because it needs to be.
  Note this is a *GitHub Actions* secret; nothing about the bucket goes
  into Secret Manager, because there is no credential to put there (see
  §11).
- `GCP_API_URL` — the `afterapply-api` Cloud Run service's URL. Doesn't
  exist yet at this point (that's exactly why step 4's bootstrap dance
  below is two runs, not one) — added after the first successful
  `deploy-backend` run.
- Optional, only if you want readable stack traces in Sentry:
  `SENTRY_ORG`, `SENTRY_PROJECT_WEB`, `SENTRY_AUTH_TOKEN`

`.github/workflows/deploy.yml` has two independent jobs, `deploy-backend`
and `deploy-web`, each deployable on its own via the `target` input
(`backend` / `web` / `both`) on a manual `workflow_dispatch`, or
automatically on a push to `main` that touches only that side's paths (see
the file's own comment for the path-filter details). For the very first
deploy, run them **in this order**, since `deploy-web`'s build needs
`deploy-backend`'s URL baked in, and closing the CORS loop needs
`deploy-web`'s URL in turn:

```bash
# 1. First run: deploy-backend only.
gh workflow run deploy.yml -f target=backend
gcloud run services describe afterapply-api --region="$REGION" --format='value(status.url)'
# → add this URL as the GCP_API_URL GitHub secret.

# 2. Second run: deploy-web only. Note the web URL:
gh workflow run deploy.yml -f target=web
gcloud run services describe afterapply-web --region="$REGION" --format='value(status.url)'

# 3. Close the CORS loop with the real web URL, then redeploy the API.
printf '%s' "https://<the-web-url-from-step-2>" \
  | gcloud secrets versions add afterapply-web-origin --data-file=-
gcloud run services update afterapply-api --region="$REGION" \
  --update-secrets=Cors__AllowedOrigins__0=afterapply-web-origin:latest
```

This is only a one-time bootstrap cost — every deploy after this, both
jobs already have what they need, and pushes to `main` deploy each side
independently based on what actually changed.

### 5. Make the services public

**Do this before testing anything** — new Cloud Run services are private
by default (require an authenticated caller), and `deploy.yml`
deliberately does *not* set `--allow-unauthenticated` (Google's own
recommendation is that CI/CD shouldn't manage this setting — see
DECISIONS.md "Sprint 13 — gerçek deploy"). Without this step every
request, including `/health`, returns a Google-generated 403, which is
easy to misdiagnose as an application bug:

```bash
gcloud run services add-iam-policy-binding afterapply-api --region="$REGION" \
  --member=allUsers --role=roles/run.invoker
gcloud run services add-iam-policy-binding afterapply-web --region="$REGION" \
  --member=allUsers --role=roles/run.invoker
```

### 6. Run migrations — do this before testing registration/login

> **As of 2026-08-27, this step is automatic** — `deploy.yml`'s
> `deploy-backend` job builds `src/AfterApply.Api/Dockerfile.migrate` into
> an `afterapply-migrate` Cloud Run Job and runs it (`gcloud run jobs
> execute --wait`) before updating the `afterapply-api` service, on every
> deploy. It reads `ConnectionStrings__Postgres` from the same
> `afterapply-postgres-connection` Secret Manager secret the api service
> uses, via its own runtime identity — the DB password never touches CI
> logs, GitHub secrets, or a local machine. See DECISIONS.md "Deploy
> pipeline'ına otomatik migration adımı" for why. The manual path below is
> kept for the very first bootstrap (before `afterapply-migrate` exists)
> and for troubleshooting.

The database schema does not exist yet at this point (Cloud SQL gives you
an empty `afterapply` database, no tables) — skipping this step is the
most common way to get a working-looking deploy that 500s on the first
real request (found exactly this way during the 2026-08-26 deploy, see
DECISIONS.md). `Program.cs` never calls `Database.Migrate()` automatically
(Sprint 7 decision, still true) — this is always a separate, explicit step.

Cloud SQL isn't reachable by Unix socket from a local machine the way
Cloud Run reaches it. **Recommended path — Cloud SQL Auth Proxy**
(no public IP exposure, no IP whitelist to remember to close afterward;
requires the proxy binary and `gcloud` authenticated locally with
Application Default Credentials — `gcloud auth application-default login`
once if `gcloud auth application-default print-access-token` fails):

```bash
# Install once: https://cloud.google.com/sql/docs/postgres/sql-proxy#install
# Port 5432 is often already taken by a local dev Postgres — use 5433.
cloud-sql-proxy --port 5433 "${PROJECT_ID}:${REGION}:afterapply-db" &
dotnet ef database update \
  --project src/AfterApply.Infrastructure --startup-project src/AfterApply.Api \
  --connection "Host=127.0.0.1;Port=5433;Database=afterapply;Username=afterapply;Password=<DB_PASSWORD from step 2>"
```

This same proxy — left running in the background — is also the way to
point a GUI client (DataGrid, etc.) at production: connect it to
`127.0.0.1:5433` (no SSL needed, the proxy tunnel is already encrypted).
Kill the `cloud-sql-proxy` process when you're done; it doesn't need to
stay up between sessions.

**Alternative — temporary authorized network**, if you can't install the
proxy binary on this machine (opens the instance to a single public IP
until you close it again — remember step 3 below):

```bash
# Your current public IPv4 (any "what's my IP" method works):
MY_IP="$(curl -s -4 https://ifconfig.me)"
gcloud sql instances patch afterapply-db --authorized-networks="${MY_IP}/32"
SQL_PUBLIC_IP="$(gcloud sql instances describe afterapply-db --format='value(ipAddresses[0].ipAddress)')"
echo "$SQL_PUBLIC_IP"
```

Then, **on your own machine, in your own terminal** (not pasted to an AI
assistant — it contains `DB_PASSWORD`):

```bash
dotnet ef database update \
  --project src/AfterApply.Infrastructure --startup-project src/AfterApply.Api \
  --connection "Host=<SQL_PUBLIC_IP>;Port=5432;Database=afterapply;Username=afterapply;Password=<DB_PASSWORD from step 2>;SSL Mode=Require;Trust Server Certificate=true"
```

Afterward, close the temporary public access again (Cloud Run itself
never used it — it connects over the private Unix-socket connector from
step 4's `--add-cloudsql-instances` flag):

```bash
gcloud sql instances patch afterapply-db --clear-authorized-networks
```

### 7. Verify it actually works

Don't stop at "the deploy went green" — confirm a real request round-trips
through the app to Postgres and back:

```bash
curl -s https://<afterapply-api-URL>/health
# expect: "Healthy"

curl -s -X POST https://<afterapply-api-URL>/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"smoke-test@example.com","password":"Test1234!","firstName":"Smoke","lastName":"Test","consentAccepted":true}' \
  -w "\nHTTP %{http_code}\n"
# expect: HTTP 201 with an accessToken in the body
```

### 8. Custom domain (optional)

Map custom domains for free automatic SSL if you have one (checklist item,
DEVELOPMENT_PLAN.md Sprint 13). Note the command group is `beta`:

```bash
# Verify domain ownership first (skip only if the domain was bought
# through Google Domains) — opens a Search Console verification flow:
gcloud domains verify yourdomain.com

gcloud beta run domain-mappings create --service afterapply-web \
  --domain yourdomain.com --region="$REGION"
gcloud beta run domain-mappings create --service afterapply-api \
  --domain api.yourdomain.com --region="$REGION"

# Each command prints the DNS records (A/AAAA for the apex domain,
# CNAME for the subdomain) to add at your registrar/DNS provider.
gcloud beta run domain-mappings describe --domain yourdomain.com \
  --region="$REGION" --format='value(status.conditions)'
```

**If your DNS is on Cloudflare** (or any other proxying DNS/CDN): add
the records with the proxy **off** ("DNS only" / grey cloud in
Cloudflare's UI, not the default orange "Proxied"). A proxied record
breaks Google's certificate issuance — this isn't a Cloud Run
peculiarity, it's inherent to layering one TLS-terminating proxy in
front of another. Certificate provisioning then takes anywhere from
~15 minutes to a few hours (`status.conditions` above shows
`CertificatePending` until it's ready, and `DomainRoutable: True` once
DNS itself resolves correctly, which is a useful signal that the DNS
side is right even before the certificate is issued).

Once the domain is live, update `NEXT_PUBLIC_API_BASE_URL`
(`GCP_API_URL` secret) and `Cors__AllowedOrigins__0`
(`afterapply-web-origin` secret) from the raw `*.run.app` URLs to the
custom domain, then redeploy both services.

### 9. Automatic deploys, and forcing a manual one

`deploy.yml` runs on every push to `main` and deploys only the side(s)
whose paths changed (a `src/**` change deploys just the API, a `web/**`
change deploys just the frontend, a change touching both deploys both) —
see DECISIONS.md 2026-09-01 for why. To force a redeploy with no code
change (e.g. after rotating a secret), dispatch it manually:

```bash
gh workflow run deploy.yml -f target=backend  # or web, or both
```

### 10. Teardown: removing the Memorystore instance (done 2026-09-06)

Kept here because it is the exact order an environment built before
2026-09-06 has to follow, and because getting it wrong takes the API
down rather than just costing money. The API refuses to start when
`ConnectionStrings:Redis` is required but missing, so the code that
stopped requiring it must be **live** before anything is deleted.

Two of these steps exist only because **removing something from
`deploy.yml` does not remove it from the deployed service** — gcloud, and
the `deploy-cloudrun` action's `secrets:` input, only change what they are
told to change. This bit us for real on 2026-09-06: the secret reference
survived the deploy, the secret was deleted underneath it, and the API kept
serving on a warm instance while every future cold start was already broken
(`secretKeyRef` resolves at container start, not at deploy). `/health` was
green the whole time it was broken, so do not treat it as proof on its own —
check the serving revision's env.

```bash
# 1. Deploy the Redis-free API first, and confirm it is actually serving.
gh workflow run deploy.yml -f target=backend
API_URL="$(gcloud run services describe afterapply-api --region="$REGION" --format='value(status.url)')"
curl -fsS "${API_URL}/health"                        # expect: Healthy

# 2. Detach the secret from the service. The deploy above does NOT do this:
#    the action merges its `secrets:` list into what the service already has,
#    so dropping the line only stops it being re-asserted. Skipping this and
#    going straight to step 4 leaves the service mounting a secret that is
#    about to stop existing — the failure lands on the next cold start, long
#    after the deploy that looked fine.
gcloud run services update afterapply-api --region="$REGION" \
  --remove-secrets=ConnectionStrings__Redis

# 3. Detach Direct VPC Egress. It existed only to reach Memorystore's
#    private IP — Cloud SQL goes over the /cloudsql Unix socket, not the
#    VPC. Same merge-not-replace story as step 2.
gcloud run services update afterapply-api --region="$REGION" --clear-network

# 4. Confirm the revision now serving traffic carries neither, BEFORE
#    deleting anything. This is the check that would have caught the
#    2026-09-06 near-miss.
REV="$(gcloud run services describe afterapply-api --region="$REGION" \
  --format='value(status.traffic[0].revisionName)')"
gcloud run revisions describe "$REV" --region="$REGION" \
  --format='yaml(spec.containers[0].env)' | grep -i redis   # expect: no match

# 5. Delete the instance. This is what stops the billing.
gcloud redis instances delete afterapply-redis --region="$REGION"

# 6. Clean up what pointed at it.
gcloud secrets delete afterapply-redis-connection
gcloud services disable redis.googleapis.com
```

### 11. Cloud Storage bucket for CV uploads (Sprint 16 — done 2026-09-07)

Uploaded CVs are the first thing this product stores outside Postgres.
The bucket is a one-time setup, like Cloud SQL in §2: `deploy.yml` only
points the service at it (`Storage__Provider` / `Storage__BucketName`),
it never creates it.

Two settings below are the ones that actually matter, and both are easy
to leave at a default that is wrong here:

- **`--public-access-prevention=enforced`** with uniform bucket-level
  access. Nothing about this design ever wants a publicly readable
  object: every download is proxied through the API, which authenticates
  the caller and checks that the row belongs to them. Enforced
  prevention means a later `gsutil iam ch allUsers:objectViewer` typed
  by mistake is refused outright rather than quietly publishing every
  CV in the bucket.
- **`--soft-delete-duration=0`** — this is the one to get right. Cloud
  Storage enables soft delete on new buckets by default with a 7-day
  retention window, so a "deleted" object is recoverable for a week. For
  ordinary data that is a safety net; for personal data a user asked us
  to erase, it means "delete" did not delete, which is exactly the
  promise `/privacy` and the help centre make. Turning it off is what
  makes the deletion real.

```bash
PROJECT_ID="$(gcloud config get-value project)"
REGION=europe-west1        # same region as Cloud SQL and both Cloud Run services

# The bucket. Regional, in the EU, so CV files never leave the region the
# rest of the personal data already lives in (PRIVACY_CHECKLIST.md).
gcloud storage buckets create "gs://afterapply-cvs" \
  --project="$PROJECT_ID" \
  --location="$REGION" \
  --default-storage-class=STANDARD \
  --uniform-bucket-level-access \
  --public-access-prevention

# Deletion has to mean deletion — see above. Verify rather than assume:
# an empty `softDeletePolicy` in the output is what "off" looks like.
gcloud storage buckets update "gs://afterapply-cvs" --clear-soft-delete
gcloud storage buckets describe "gs://afterapply-cvs" \
  --format='yaml(name,location,softDeletePolicy,iamConfiguration)'

# Object access for the runtime service account only — scoped to this one
# bucket, not granted project-wide. objectAdmin (not objectCreator or
# objectViewer) because the API does all three: write on upload, read on
# download, delete on removal and on account deletion.
RUNTIME_SA="$(gcloud iam service-accounts list \
  --filter='email~compute@developer' --format='value(email)')"
gcloud storage buckets add-iam-policy-binding "gs://afterapply-cvs" \
  --member="serviceAccount:${RUNTIME_SA}" \
  --role="roles/storage.objectAdmin"
```

**Already run, 2026-09-07** against project `ekariyerim`. Verified after the fact:
`public_access_prevention: enforced`, `uniform_bucket_level_access: true`, an empty
`soft_delete_policy` (i.e. off), `EUROPE-WEST1`/regional, and the runtime service account
(`188370748893-compute@developer.gserviceaccount.com`, which is what `afterapply-api` actually
runs as) holding `roles/storage.objectAdmin` on this bucket alone. A write/read/delete round-trip
through the JSON API succeeded and an unauthenticated read of the same object answered `401`.
The commands are kept here because they are the record of how it was built, and what to repeat
in a second environment.

One property worth knowing rather than rediscovering: a bucket always carries legacy
project-role bindings (`projectOwner`/`projectEditor` -> `legacyObjectOwner`, `projectViewer` ->
`legacyObjectReader`), and uniform bucket-level access does not remove them. So anyone with a
project-level role can read CV objects out of band. That is the project owner and the two service
accounts today, which is acceptable — but it means bucket IAM is not the whole access story if
project membership ever widens.

Then deploy the API so the new `Storage__*` env vars reach the service,
and confirm they are on the revision actually serving traffic — same
check as §10 step 4, for the same reason (a deploy that "succeeded" is
not proof the running revision has them):

```bash
gh workflow run deploy.yml -f target=backend
REV="$(gcloud run services describe afterapply-api --region="$REGION" \
  --format='value(status.traffic[0].revisionName)')"
gcloud run revisions describe "$REV" --region="$REGION" \
  --format='yaml(spec.containers[0].env)' | grep -i storage   # expect both vars
```

**No new Secret Manager entry** — this is the part worth being precise
about, because "bucket" and "secret" sound like they should go together.
There is no credential to store: on Cloud Run the API authenticates to
Cloud Storage as its own runtime service account through Application
Default Credentials (a short-lived token from the metadata server), so
there is no key file, nothing to rotate, and nothing that could leak.
Access is granted entirely by the IAM binding above. The bucket's *name*
does live in a GitHub Actions secret (`GCP_CV_BUCKET`, §4) — but only for
consistency with `GCP_PROJECT_ID`/`GCP_REGION`; it is an address, not a
credential, and an unauthenticated read of an object in this bucket
answers `401` whether or not you know the name.

Had this been built on signed URLs instead, it *would* have needed one of
the two: either a service-account JSON key in Secret Manager, or
`roles/iam.serviceAccountTokenCreator` on the runtime account so it could
sign through the IAM Credentials API. Proxying downloads through the API
avoids both — see DECISIONS.md 2026-09-07.

**Local development and tests** do not touch any of this. `Storage:Provider`
defaults to `FileSystem`, which writes under the OS temp directory, and
`AddDocumentStorage` refuses that provider outright when
`ASPNETCORE_ENVIRONMENT=Production` — Cloud Run's filesystem is in-memory
and per-instance, so an upload written there would vanish on the next
revision and be invisible to every other instance. Failing at startup is
the only way that mistake surfaces before a user's CV is lost.

### 12. Vertex AI for the CV scan's content notes (V6 layer B — 2026-09-10)

The CV scan's score never involves a model and never will (see
`DECISIONS.md` 2026-09-10). The *content notes* beside it do: they are
written by Gemini through Vertex AI. The code ships with the feature
**off** — `CvScan:LlmEnabled` is `false` and the optional consent box is
not even rendered — so nothing below is needed to run the product. Do it
when you want layer B on.

Three properties of this setup are privacy decisions rather than
engineering ones, and all three are visible in the code:

- **The region is in the URL.** `VertexCvReviewProvider` calls
  `https://{location}-aiplatform.googleapis.com/...`, with the location
  coming from `CvScan:Review:Location`. Keep it in the EU — the same
  region the rest of the personal data already sits in — so no new
  country appears in `/privacy`. **Never set it to `global`**: that is
  precisely the guarantee it would drop.
- **There is no API key.** Authentication is Application Default
  Credentials, i.e. the Cloud Run runtime service account, so there is
  nothing to rotate or to leak and access is revoked by removing one IAM
  binding.
- **Paid Vertex, not the AI Studio free tier.** The free tier's terms
  allow the content to be used to improve the product; paid Vertex does
  not train on it. CV text may not go through the free tier at all.

```bash
PROJECT_ID="$(gcloud config get-value project)"
PROJECT_NUMBER="$(gcloud projects describe "$PROJECT_ID" --format='value(projectNumber)')"
RUNTIME_SA="${PROJECT_NUMBER}-compute@developer.gserviceaccount.com"

# 1. Enable the API.
gcloud services enable aiplatform.googleapis.com --project="$PROJECT_ID"

# 2. Let the runtime service account call models. roles/aiplatform.user is
#    the smallest role that can run inference; it grants no training, no
#    tuning and no data access beyond the call itself.
gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:${RUNTIME_SA}" --role="roles/aiplatform.user"

# 3. Check the model is actually served from the region you pinned. Model
#    availability is per-region and changes; a model that is not there
#    answers 404 and layer B degrades to "unavailable" (which is safe, but
#    means nobody ever sees a note).
gcloud ai models list --region=europe-west1 --project="$PROJECT_ID" 2>/dev/null | head
```

Then turn it on for the API service — both settings, because the code
requires a project id as well as the flag before it will offer the
consent box at all:

```bash
gcloud run services update afterapply-api --region=europe-west1 \
  --update-env-vars=CvScan__LlmEnabled=true,CvScan__Review__ProjectId="$PROJECT_ID"
```

`CvScan__Review__Location`, `__Model`, `__DailyRequestCeiling`,
`__MaxInputCharacters` and `__TimeoutSeconds` all have defaults in
`appsettings.json` and are env-var overridable the same way. The daily
ceiling is what bounds a day's spend: it is counted from
`CvScanResults.ContentNotesRequested`, so it holds across instances and
revisions rather than per process. Reaching it degrades layer B and
leaves the score alone.

**Before turning it on, run the eval.** It scores a synthetic corpus of
CVs with known weaknesses against the real API and prints every note:

```bash
CV_REVIEW_EVAL=1 CvScan__Review__ProjectId="$PROJECT_ID" \
  dotnet test tests/AfterApply.IntegrationTests --filter FullyQualifiedName~CvReviewEval
```

It needs local Application Default Credentials
(`gcloud auth application-default login`) and is the only test in that
assembly allowed to open a socket — everything else is blocked by
`NoOutboundHttpStartup`. Without `CV_REVIEW_EVAL=1` it does nothing.

**Turning it off** is one env var (`CvScan__LlmEnabled=false`), takes
effect on the next revision, and costs nothing but the notes: the score,
the findings and the page all keep working, because they never depended
on the model.

### 13. Weekly job-source sweep (LinkedIn) — shipped off (2026-09-12)

**Branch state:** this lives on `feat/linkedin-job-source`, unmerged, until the PayTR payment
integration is settled — the feature is part of the paid plan and has no reason to exist on `main`
before people can pay for it (DECISIONS.md 2026-09-12, "Merge kararı"). Rebase on `main` and re-run
both test suites before merging.

Nothing to provision: no secret, no bucket, no API key. The sweep fetches LinkedIn's public
job listing over plain HTTPS from the Cloud Run egress IP (the same path the company enrichment
already uses). It ships **off** — `JobSources:Enabled=false` in `appsettings.json` — and stays
off until the paid plan is live.

To turn it on:

1. Confirm the privacy policy is deployed with the job-matching paragraph (`/privacy`,
   "Cross-border data transfer", third case) — it names LinkedIn as a recipient of the user's
   criteria, and the flag must never be on without it.
2. Flip `JobSources__Enabled=false` to `true` in `.github/workflows/deploy.yml`'s `env_vars` block
   (it is declared there so every deploy re-asserts it) and let the deploy run.
3. Grant the first users: `PUT /api/admin/pro/entitlements/{userId}` with an `activeUntil`; the
   sweep only runs for users with an active entitlement, saved criteria, a CV and a sign-in in the
   last 30 days.
4. After the first Monday 04:00 UTC run, read `GET /api/admin/job-sources/usage`: `requestsToday`
   against `maxRequestsPerDay`, and `cooldownUntil` — non-null means LinkedIn answered 429/403 or a
   login wall and the sweep has stopped itself for 24 h. If that happens on the first run, the
   Cloud Run IP is being refused and the feature should go back off rather than be retried harder.
5. Cloud Run at `min-instances=0` can miss the 04:00 tick; Hangfire runs a missed recurring job at
   the next server start, and the sweep is idempotent within an ISO week, so a late run is fine.
   A Cloud Scheduler ping at 04:05 is the cheap fix if it matters.

Logs carry counts only — never a title, a location or a URL; the HttpClient's request logging is
removed for this client for the same reason.
