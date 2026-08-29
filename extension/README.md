# e-kariyerim Browser Extension (Sprint 9, kariyer.net support added later)

A Manifest V3 Chrome/Edge extension that turns a LinkedIn or kariyer.net job posting page into a
tracked e-kariyerim application with one click ("I Applied") — see
`ekariyerim-intelligence-platform-plan.md` §11 and `DECISIONS.md`'s Sprint 9 entry for the
original (LinkedIn-only) design.

## Setup

1. **Generate an access token.** In the e-kariyerim web app, go to Settings → Browser Extension,
   click "Generate Token", and copy the value shown (`aa_pat_...`) — it's shown only once. This
   token grants full access to your account, same as being logged in; only paste it into your own
   extension install.
2. **Load the extension unpacked.**
   - Chrome/Edge: open `chrome://extensions` (or `edge://extensions`), enable **Developer mode**,
     click **Load unpacked**, and select this `extension/` folder.
3. **Configure it.** Click the e-kariyerim icon in the toolbar → if no token is set yet, click
   "Open Settings" (or right-click the icon → Options). Set the **API base URL** (default
   `http://localhost:5151` for local dev) and paste the **access token**, then Save.

## Using it

Navigate to a LinkedIn job posting page (`linkedin.com/jobs/view/<id>/...`) or a kariyer.net job
posting page (`kariyer.net/is-ilani/<slug>-<id>`) and click the e-kariyerim toolbar icon. The popup
best-effort scrapes the company, title, and location from the page — **all fields are editable
before you submit**, so an imperfect scrape never becomes a wrong submission. Click **I Applied**
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

`manifest.json`'s `host_permissions` currently covers `linkedin.com`, `kariyer.net`, and
`http://localhost/*` (any local port). If you point the extension's Settings → API base URL at a
non-localhost API (a real deployment), add that origin to `host_permissions` too — a Manifest V3
extension page's `fetch()` is only exempt from CORS for origins explicitly listed there; an
unlisted origin gets blocked the same as an ordinary web page's cross-origin fetch would be (found
via manual testing — see DECISIONS.md Sprint 9).

## Theming

`popup.css` defines light/dark tokens mirroring the web app's own Tailwind palette
(`web/src/components/ui/Button.tsx`, `Input.tsx`) so the extension reads as part of the same
product. `theme.js` (shared by `popup.js` and `options.js`) applies the OS's `prefers-color-scheme`
by default and persists an explicit toggle choice via `storage.js`'s `getTheme`/`saveTheme` — a
per-install preference, like the API settings, with no account sync (an extension install has no
session to sync to).

## Publishing to the Chrome Web Store

See `store-listing/` for the listing copy, privacy policy draft, permission justifications, ready
screenshots, and a step-by-step `PUBLISHING_CHECKLIST.md`. This wasn't done as part of the sprint
that built it (DEVELOPMENT_PLAN.md Sprint 9 explicitly scoped it out as a separate, later step) —
`store-listing/` prepares everything needed, but actually submitting still requires publishing the
privacy policy at a real URL and someone with access to the Developer Dashboard to click through
the checklist.

## Not in this sprint

- Employment type is not scraped (LinkedIn's job header doesn't expose it directly) — created
  applications default to `FullTime`, the same known limitation as generic CSV import
  (DECISIONS.md Sprint 4).
