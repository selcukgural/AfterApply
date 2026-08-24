# AfterApply

Job Application Tracker + Personal Analytics. See `afterapply-intelligence-platform-plan.md`
for the product/technical spec, `DEVELOPMENT_PLAN.md` for the sprint roadmap,
and `DECISIONS.md` for architecture/technical decisions.

**Status: Sprint 1 (Identity + Core Domain).** Registration/login/refresh/
logout, Company/Job/Application/ApplicationEvent/ApplicationStatusHistory,
Application CRUD + status transitions + timeline. No UI, no analytics, no
import pipelines yet — see `DEVELOPMENT_PLAN.md` for what's next.

## Architecture

Clean Architecture, layer-first modular monolith:

```
Domain            (no project references)
Application  ──►  Domain
Infrastructure ──► Application, Domain
Api          ──►  Infrastructure, Application, Domain
```

Modules (Applications, Companies, Jobs, Identity, Imports, Analytics,
Notifications) live as namespaces/folders inside each layer, not as separate
projects — see `DECISIONS.md` #1. Layer dependency direction is enforced by
NetArchTest (`tests/AfterApply.UnitTests/Architecture`).

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
7. `ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/AfterApply.Api`
8. `curl -i http://localhost:5151/health` → expect `200 Healthy`

### Trying the API

```bash
curl -X POST http://localhost:5151/api/auth/register -H "Content-Type: application/json" -d \
  '{"email":"you@example.com","password":"P@ssw0rd123!","firstName":"You","lastName":"There"}'
# → { "accessToken": "...", "refreshToken": "...", ... }

curl -X POST http://localhost:5151/api/applications -H "Authorization: Bearer <accessToken>" \
  -H "Content-Type: application/json" -d \
  '{"companyName":"Acme","jobTitle":"Backend Engineer","employmentType":"FullTime","appliedAt":"2026-08-20T10:00:00Z"}'
```

See `/api/auth/*`, `/api/users/me`, `/api/applications/*` in
`src/AfterApply.Api/Endpoints` for the full surface, or browse
`/openapi/v1.json` (Development only).

## Container environment (podman compose / docker compose)

Uses offset host ports (**5434** for Postgres, **6382** for Redis) to reduce
the odds of colliding with a native Postgres/Redis or another local
project's containers — check for conflicts on your machine and adjust
`docker-compose.yml` if needed.

1. `cp .env.example .env` and fill in `POSTGRES_PASSWORD`
2. `podman compose up --build` (or `docker compose up --build`)
3. `curl -i http://localhost:8080/health`
4. `podman compose down` (add `-v` to also drop the postgres volume)

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
  containers may occasionally need manual `podman rm` cleanup.
- Everything: `dotnet test AfterApply.slnx`

## Configuration

The app reads `ConnectionStrings:Postgres` and `ConnectionStrings:Redis` from
the standard ASP.NET Core configuration chain — no connection string is
hard-coded anywhere. Locally, set them via `dotnet user-secrets` (see above).
In the container environment, they're supplied as `ConnectionStrings__Postgres`
/ `ConnectionStrings__Redis` environment variables in `docker-compose.yml`.
If unset, the API fails fast on startup with an explanatory error instead of
silently connecting to the wrong database.
