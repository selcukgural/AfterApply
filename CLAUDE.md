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

# Async policy

Asenkron bir operasyon varsa **thread bloklanmaz** — her yerde async/await kullanılır. Bu
standing bir kuraldır, her yeni kod ve dokunduğun her mevcut kod için geçerlidir.

- Yasak (sync-over-async): `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, `Task.WaitAll`,
  `Task.Run(...).Result`, senkron bir metodu async iş yapmak için `Task.Run` ile sarmak.
- Async overload varsa senkron olanı kullanma: EF Core (`ToListAsync`, `FirstOrDefaultAsync`,
  `SaveChangesAsync`, `AnyAsync`...), `HttpClient` (`ReadAsStringAsync`, `SendAsync`),
  `Stream`/`File` (`ReadAsync`, `WriteAsync`), Redis, Hangfire job gövdeleri, MediatR/handler'lar.
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

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
