# e-kariyerim Browser Extension (Sprint 9, kariyer.net support added later)

A Manifest V3 Chrome/Edge extension that turns a LinkedIn or kariyer.net job posting page into a
tracked e-kariyerim application with one click ("I Applied") — see
`ekariyerim-intelligence-platform-plan.md` §11 and `DECISIONS.md`'s Sprint 9 entry for the
original (LinkedIn-only) design.

## Setup

1. **Load the extension unpacked.**
   - Chrome/Edge: open `chrome://extensions` (or `edge://extensions`), enable **Developer mode**,
     click **Load unpacked**, and select this `extension/` folder.
2. **Connect it.** Click the e-kariyerim icon in the toolbar → **Connect**. The extension asks the
   API for a short pairing code, opens the web app's `/pair?code=…` page in a tab, and collects the
   access token once you confirm there. Nothing to copy or paste, and no account needed up front —
   the confirmation page can register one and come straight back.
3. **Pointing at a local API.** The pairing endpoints live on whatever **API base URL** is set, so
   for local dev open Settings, expand *Enter a key by hand (advanced)*, set the base URL (e.g.
   `http://localhost:5151`) and Save first — Connect then pairs against that API. Entering a token
   by hand still works there too, which is what the disclosure is for.

The token an approved pairing produces is `Extension`-scoped (only the endpoints this extension
calls) and expires after `PersonalAccessTokens:LifetimeDays`, 90 by default. The extension stores
that expiry alongside it and warns in the last two weeks; before 0.7.0 it did not, which is why a
connection used to stop working one day with no explanation.

> **Testing against a local API needs one temporary manifest edit.** `host_permissions` ships
> without `http://localhost/*` on purpose — a published extension asking to read and change data on
> localhost is a permission users shouldn't have to grant for a feature they'll never use. Without
> it, MV3 subjects the popup's `fetch` to CORS, the API only allows `http://localhost:3000`, and
> every submit fails with the generic "could not reach e-kariyerim". So while testing locally, add
> `"http://localhost/*"` to `host_permissions`, reload the extension — **and take it back out
> before committing.**

## Using it

Navigate to a LinkedIn job posting page (`linkedin.com/jobs/view/<id>/...`) or a kariyer.net job
posting page (`kariyer.net/is-ilani/<slug>-<id>`) and click the e-kariyerim toolbar icon. The popup
best-effort scrapes the company, title, and location from the page — and, on a LinkedIn posting
that shows a hiring team, the job poster's name and profile URL — **all fields are editable before
you submit**, so an imperfect scrape never becomes a wrong submission.

The hiring-team scrape is scoped to `.hirer-card__hirer-information`, deliberately *not* to "the
first `/in/` link on the page": a LinkedIn job page also renders Premium's "People you can reach
out to" block, which lists school alumni and 3rd-degree connections who have nothing to do with the
posting. Verified live on 2026-09-06 across three postings — the alumni-only one exposed two
profile links and this selector matched none of them, while the posting with a real hiring team
resolved to the right person. Most postings have no such card at all (it is poster opt-in), and
kariyer.net has no counterpart, so an empty HR field is the normal case. Click **I Applied**
to create the application. Clicking it again on the same job page is safe — the backend dedupes by
job URL and returns your existing application instead of a duplicate
(`POST /api/applications/from-extension`, see `ApplicationService.CreateFromExtensionAsync`). The
backend also classifies which site a submitted URL came from (`JobPostingSourceResolver.cs`) to
set `Job.Source` (`LinkedIn` / `KariyerNet`) and extract that site's own job id as
`Job.ExternalId` — `Application.Source` is always `BrowserExtension` regardless of the originating
site, since that field tracks how the row was created, not the job's data provenance.

## Known limitation: scraping selectors are best-effort

Neither site's DOM class names are a stable public API and both change over time. The kariyer.net
selectors in `popup.js`'s `scrapeKariyerNetJob()` were verified against a live posting; LinkedIn's
in `scrapeLinkedInJob()` were not verified against a live page in the session that wrote them (no
automated scraping of a real third-party site was performed then). Either way, if a site changes
its markup, scraping may return empty fields — this degrades gracefully (empty inputs the user
fills in by hand) rather than submitting wrong data, but the selectors likely need periodic
updates. For LinkedIn, a `<title>`-based fallback (job page titles are typically
`"<Title> hiring at <Company> | LinkedIn"`) covers the most common breakage.

## Known limitation: `host_permissions` must list the API origin

`manifest.json`'s `host_permissions` lists `linkedin.com`, `kariyer.net`, `mail.google.com` and the
two API origins (`api.ekariyerim.com` and the Cloud Run URL). If you point the extension's
Settings → API base URL at anything else — a local API at `http://localhost:5151`, say — add that
origin to `host_permissions` too, or every call fails: a Manifest V3 **extension page or service
worker**'s `fetch()` is exempt from CORS only for origins explicitly listed there, and an unlisted
origin is blocked exactly like an ordinary web page's cross-origin fetch (found via manual testing
— see DECISIONS.md Sprint 9). Keep such a local-only entry out of what you upload to the store.

A **content script** is a stricter case still, and the reason `background.js` exists: its `fetch()`
runs on behalf of the page it was injected into, so it is subject to *that page's* CORS and
`host_permissions` does not exempt it at all ("Cross-origin requests are always treated as such in
content scripts, even if the extension has host permissions"). Every API call a content script
needs therefore goes through the service worker — never straight out of the content script.

## Gmail Scanning

`gmail-scan.js` is a declared `content_scripts` entry (matching `https://mail.google.com/*`,
loaded alongside `local-filter-config.js`) — the extension's only script that isn't click-triggered
`scripting.executeScript`, needed because Gmail is a single-page app with no full reload between
the inbox and an opened thread. Off by default: it checks the `afterapply_gmail_scan_enabled`
flag (`storage.js`'s `getGmailScanEnabled`/`setGmailScanEnabled`, a checkbox on the Options page)
before reading anything, and no-ops entirely while it's off.

Once enabled, it reads only the currently-open/expanded thread (`span[email]` for the sender,
`h2.hP` for the subject, `div.a3s` for the body, capped at 2000 chars — verified live against real
Gmail threads, see the plan this shipped under), scores it locally with `afterApplyScoreSignal` (a
JS mirror of the backend's `RecruitmentSignalAnalyzer.Analyze` shape — weighted phrase-category
hits, capped, plus known-domain/link-domain bonuses), and only for a thread scoring above the
fetched threshold does it ask `background.js` to POST the extracted signal (never the raw email) to
`POST /api/email-forwarding/extension-signal`. `local-filter-config.js` caches the scoring
vocabulary the worker fetches from `GET /api/email-forwarding/local-filter-config` (ETag-revalidated,
so tuning weights/phrases/domains only needs an `appsettings.json` edit + backend redeploy, never a
new extension release) with a small bundled fallback for first-run/offline. Both files are plain
scripts, not ES modules — Chrome MV3 content scripts have no `import`/`export` support.

Neither of them makes a network request itself, and neither reads the access token: both go through
`background.js`, which owns the token and answers only a fixed two-route allow-list. That indirection
is not a style choice — a content script's own `fetch()` to the API is blocked by the page's CORS
before it leaves the browser (see the `host_permissions` limitation above). Between 0.5.0 and 0.7.0
it was one, and Gmail Scanning silently produced nothing at all in those builds; fixed in 0.8.0.

This is now the extension's only email-signal intake path — see
`EmailForwardingService.ProcessExtensionSignalAsync` (backend route namespace stays
`/api/email-forwarding` for compatibility with already-installed extension versions; it no longer
means "forwarding"). The earlier forward-all-inbox-to-us design (`EmailProvider.Forwarding`, a
step-by-step Gmail-forwarding guide page, and the Cloudflare Email Worker relaying it) was removed
entirely — see DECISIONS.md — after live testing showed it and Gmail-filter-based alternatives
couldn't stay both bounded and complete. `EmailProvider` now has a single member, `Extension`.

## Theming

`popup.css` defines light/dark tokens mirroring the web app's own Tailwind palette
(`web/src/components/ui/Button.tsx`, `Input.tsx`) so the extension reads as part of the same
product. `theme.js` (shared by `popup.js` and `options.js`) applies the OS's `prefers-color-scheme`
by default and persists an explicit toggle choice via `storage.js`'s `getTheme`/`saveTheme` — a
per-install preference, like the API settings, with no account sync (an extension install has no
session to sync to).

## Publishing to the Chrome Web Store

The item is live:
<https://chromewebstore.google.com/detail/e-kariyerim-%E2%80%94-job-import/lemdkeljacdgbbcmefpbggphnhhciimi>.
It went up around 2026-09-01 carrying `0.4.0`; **`0.5.0` and `0.6.0` have not been uploaded**, so
what users install today has no Gmail Scanning and no HR-contact capture.

`store-listing/` holds the listing copy, the privacy policy source, permission justifications, the
1280×800 screenshots, and `PUBLISHING_CHECKLIST.md` — start there for an upload. The privacy policy
is published from the web app at `/extension-privacy`
(`web/src/app/[locale]/(public)/extension-privacy/page.tsx` + the `extensionPrivacy` block in
`web/messages/*.json`); `store-listing/PRIVACY_POLICY.md` is its source text and the two must be
edited together.

## Not in this sprint

- Employment type is not scraped (LinkedIn's job header doesn't expose it directly) — created
  applications default to `FullTime`, the same known limitation as generic CSV import
  (DECISIONS.md Sprint 4).
- Gmail Scanning (see above) is Gmail-only, by construction — it reads Gmail's own DOM. No
  Outlook/other-provider equivalent exists.
