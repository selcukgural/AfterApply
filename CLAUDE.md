# Product naming

The product's name is **e-kariyerim**, not "AfterApply". "AfterApply" was the bootstrap/prototype
name used when the repo was first created and must not appear in any new work: user-facing
strings, User-Agent headers, comments, documentation, commit messages, extension listing copy,
etc. — use "e-kariyerim" instead.

This split is **permanent and intentional**, not a migration in progress: the .NET
solution/project/namespace names (`AfterApply.Domain`, `AfterApply.Api`, ...), the repo/GitHub
directory name, and real GCP/Cloud Run/Cloud SQL/Postgres resource identifiers
(`afterapply-api`, `afterapply-db`, secrets, `.github/workflows/*.yml`, `docker-compose.yml`,
`.env*.example`, `extension/manifest.json`'s old host permission, `extension/storage.js`'s
`chrome.storage` keys) stay `AfterApply`/`afterapply-*` forever — internal/infra identifiers,
invisible to a user, same bucket as a variable name. Never rename those on your own initiative.
Everywhere a human actually reads the name (docs, comments, UI text, User-Agent strings, tool
display names) — use "e-kariyerim".

**Before touching naming/branding again, check `DECISIONS.md` first** — it's the canonical
decision log for this project and already has entries covering exactly this split ("Sprint 15
kararları ve bulguları (ekariyerim rebrand + logo)" and the later "backend/dış-servis
genişletmesi" follow-up). Don't re-ask the user to reconfirm a scope decision already recorded
there.

# Language of git artefacts

**Commit messages and pull request titles/descriptions are written in English**, always — even
when the conversation that produced the change was in Turkish. They are read by contributors,
by GitHub's own UI and by tooling, and the rest of the history is already English; a Turkish
one in the middle is the odd one out.

This covers git artefacts only. It does not change the language of anything else: `DECISIONS.md`
stays Turkish, and user-facing strings keep shipping in both `tr` and `en` as they do today.

# Async policy

Asenkron bir operasyon varsa **thread bloklanmaz** — her yerde async/await kullanılır. Bu
standing bir kuraldır, her yeni kod ve dokunduğun her mevcut kod için geçerlidir.

- Yasak (sync-over-async): `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, `Task.WaitAll`,
  `Task.Run(...).Result`, senkron bir metodu async iş yapmak için `Task.Run` ile sarmak.
- Async overload varsa senkron olanı kullanma: EF Core (`ToListAsync`, `FirstOrDefaultAsync`,
  `SaveChangesAsync`, `AnyAsync`...), `HttpClient` (`ReadAsStringAsync`, `SendAsync`),
  `Stream`/`File` (`ReadAsync`, `WriteAsync`), Hangfire job gövdeleri, MediatR/handler'lar.
- Async zinciri uçtan uca taşı: endpoint/handler → service → repository hepsi `Task`/`ValueTask`
  döndürür; `CancellationToken` parametre olarak alınıp aşağı geçirilir.
- `async void` yok (framework event handler'ları dışında). Sadece bir Task döndürüyorsan
  gereksiz `async/await` yerine Task'i doğrudan döndürmek serbest, ama try/finally veya `using`
  varsa `await` et.
- Frontend/extension tarafında da aynı kural: senkron XHR, blocking loop, `await` edilmeyen
  promise (floating promise) yok — hata yutulur.

**İstisna:** gerçekten async alternatifi olmayan yerler — composition root (`Program.cs` kurulum
kodu), constructor içi zorunlu ilklendirme, `Dispose` yolları, bazı test yardımcıları. Bu
durumlarda blokla ama **neden mecbur kalındığını tek satır yorumla belirt**; sessizce `.Result`
bırakma.

# Testing policy

Every development change must ship with tests when the change is testable that way. Tests are
part of the deliverable, not a follow-up.

- **Unit tests** go in `tests/AfterApply.UnitTests` (no container runtime needed). Add them for
  new or changed domain logic, services, validators, parsers, classifiers, mappers, etc.
- **Integration tests** go in `tests/AfterApply.IntegrationTests` (Testcontainers-Postgres via
  podman). Add them for new or changed API endpoints, EF/persistence behaviour, Hangfire jobs,
  auth/rate-limit/policy wiring — anything that only proves itself against a real host + DB.
- **Frontend / extension** changes: add tests where a test harness exists for that area;
  otherwise state in the summary that no harness exists rather than silently skipping.
- If a change is genuinely not testable (pure config, docs, infra identifiers), say so
  explicitly in the summary instead of omitting tests quietly.
- Run cadence: unit tests continuously during development; run the podman-backed integration
  suite once at the end of a batch of work, not after every edit (see `README.md`
  "Running tests").

# Security baseline (OWASP)

Every change — backend, web frontend, Chrome extension alike — must stay as close to OWASP's
critical items as the change allows. Application security and user privacy are part of "done",
not a later hardening pass: the product holds job-application history, HR contact details and
scanned email content, which is personal, sensitive and impossible to un-leak.

On each change, check the categories it actually touches:

- **Broken access control:** every endpoint scoped to the calling user's id; no IDOR — never trust
  a route/body id without an ownership check. Extension-token routes stay as narrow as they need.
- **Injection / XSS:** parameterised EF queries only; never `innerHTML` with server data or
  page-scraped content — LinkedIn/kariyer.net/Gmail text is untrusted input.
- **Auth & session:** no secrets or tokens in logs, URLs or client-visible payloads. The one
  intentional exception is the public OAuth client ids on `/api/config` — settled, don't re-raise.
- **Sensitive data exposure:** don't widen an API response "because it's handy"; return the
  minimum the caller needs; keep PII out of error text and telemetry.
- **SSRF:** company-enrichment and job-page fetches take URLs from user data — validate
  scheme/host before fetching.
- **Config:** security headers, CORS, rate limits and validation stay on; never relax them to make
  a test or a local run easier.

Flag anything you can't fix inside the change's scope rather than leaving it silent.

# Chrome extension release policy

A change under `extension/` is a release, not just a code edit. In the same change:

1. **Bump `"version"` in `extension/manifest.json`.** The Web Store rejects a re-upload of a
   version already used, and the popup/Settings footer renders this number (`version.js`), so it
   is what a bug report will quote back at you. The item is already live, so every upload is an
   update — see `extension/store-listing/PUBLISHING_CHECKLIST.md` for the state of the listing.
2. **Update the publish material the change invalidates**, under `extension/store-listing/`:
   `PERMISSIONS_JUSTIFICATION.md` when `permissions`/`host_permissions`/`content_scripts` or the
   data the extension sends changes (its data-usage table is what the Dashboard's Privacy
   practices tab gets), `PRIVACY_POLICY.md` when what is stored or sent changes — **and with it
   the published page it is the source for, `/extension-privacy` in the web app; the two must
   never drift** — `LISTING.md` for user-facing copy (both TR and EN), and `PUBLISHING_CHECKLIST.md`
   when the state of the listing moves.
3. **Reshoot the screenshots the change stales, before committing.** Extension UI changes reach
   two sets of public images: `extension/store-listing/screenshots/*.png` (Web Store assets, built
   from the `scene-*.html` compositions, which carry *copied* markup and so never update
   themselves) and `web/public/help/screenshots/chrome-extension-{popup,options}.png` (help
   centre, shot from the real pages). Recipes for both are in `screenshots/README.md`.
4. **Build the store package:**
   `rm -f e-kariyerim-extension.zip && cd extension && zip -r ../e-kariyerim-extension.zip . -x "store-listing/*" -x "README.md" -x "*.DS_Store"`

Also standing: the backend must keep working with **every shipped extension build** —
`/from-extension` is additive-only. See `DECISIONS.md` 2026-09-06.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
