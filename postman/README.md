# AfterApply API — Postman collection

Nothing under this directory is hand-maintained except `environments/*.json`. The collection
itself is a build artifact, generated fresh every time from the API's own OpenAPI document — if
you edit `collection.json` directly, the next generation run overwrites your changes.

## How it stays in sync with the code

1. `dotnet build src/AfterApply.Api` emits `postman/openapi/openapi.json` as a build side effect
   (see `AfterApply.Api.csproj`'s `OpenApiDocumentsDirectory` and
   `DependencyInjection.IsOpenApiDocumentGeneration`). No live Postgres/Redis needed for this step
   — it runs your Program.cs's route registration through a mock server, not a real one.
2. `npm run generate` (this directory) converts that OpenAPI document into `collection.json` and
   layers in what OpenAPI alone doesn't carry: collection-level bearer auth wired to
   `{{accessToken}}`, login/register/refresh test scripts that keep that variable populated after
   a real call, and a baseline status-code + JSON-content-type assertion on every request.
3. CI (`.github/workflows/ci.yml`'s `api-contract` job) does both of the above, then boots the
   real stack via `docker compose` and runs the freshly generated collection against it with
   Newman (`newman run collection.json -e environments/ci.json --bail`). A missing endpoint, a
   wrong parameter, or a response that doesn't match what's documented fails the build.
4. Only if that passes, and only on `main`, the job publishes the collection *and* the Local/
   Production environments to Postman Cloud (`npm run publish` / `publish:environments`) — so the
   shared team workspace only ever reflects a commit that's already been verified against a live
   API. `environments/ci.json` is CI-only and never published; it's meaningless outside the
   docker-compose stack it points at.

## Local usage

```bash
dotnet build src/AfterApply.Api          # from the repo root — emits openapi/openapi.json
cd postman && npm ci && npm run generate # writes collection.json
```

Import `collection.json` plus `environments/local.json` into Postman (desktop or web), select the
"AfterApply - Local" environment, and run the Auth folder's Login (or Register) request first —
its test script populates `accessToken` for every other request in the collection automatically.

If instead you're using the copy already synced to Postman Cloud (see below), just pick "AfterApply
- Local" or "AfterApply - Production" from Postman's environment selector — `baseUrl` is already
filled in for both; no import needed.

## Publishing to Postman Cloud manually

```bash
POSTMAN_API_KEY=<your key> npm run publish               # collection
POSTMAN_API_KEY=<your key> npm run publish:environments   # Local + Production environments
```

Both look up their target by name in your default workspace (or `POSTMAN_WORKSPACE_ID` if set) and
update it in place, creating it on first run. This is what CI does automatically on every push to
`main`. Production's `baseUrl` is the API's actual Cloud Run URL — update
`environments/production.json` (and re-publish) if that ever changes.
