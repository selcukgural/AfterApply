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
