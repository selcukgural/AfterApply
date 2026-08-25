# AfterApply Browser Extension (Sprint 9)

A Manifest V3 Chrome/Edge extension that turns a LinkedIn job posting page into a tracked
AfterApply application with one click ("I Applied") — see
`afterapply-intelligence-platform-plan.md` §11 and `DECISIONS.md`'s Sprint 9 entry for the design.

## Setup

1. **Generate an access token.** In the AfterApply web app, go to Settings → Browser Extension,
   click "Generate Token", and copy the value shown (`aa_pat_...`) — it's shown only once. This
   token grants full access to your account, same as being logged in; only paste it into your own
   extension install.
2. **Load the extension unpacked.**
   - Chrome/Edge: open `chrome://extensions` (or `edge://extensions`), enable **Developer mode**,
     click **Load unpacked**, and select this `extension/` folder.
3. **Configure it.** Click the AfterApply icon in the toolbar → if no token is set yet, click
   "Open Settings" (or right-click the icon → Options). Set the **API base URL** (default
   `http://localhost:5151` for local dev) and paste the **access token**, then Save.

## Using it

Navigate to a LinkedIn job posting page (`linkedin.com/jobs/view/<id>/...`) and click the
AfterApply toolbar icon. The popup best-effort scrapes the company, title, and location from the
page — **all fields are editable before you submit**, so an imperfect scrape never becomes a wrong
submission. Click **I Applied** to create the application. Clicking it again on the same job page
is safe — the backend dedupes by job URL and returns your existing application instead of a
duplicate (`POST /api/applications/from-extension`, see `ApplicationService.CreateFromExtensionAsync`).

## Known limitation: scraping selectors are best-effort

LinkedIn's DOM class names are not a stable public API and change over time; the selectors in
`popup.js`'s `scrapeLinkedInJob()` were not verified against a live page in this session (no
automated scraping of a real third-party site was performed). If LinkedIn changes their markup,
scraping may return empty fields — this degrades gracefully (empty inputs the user fills in by
hand) rather than submitting wrong data, but the selectors likely need periodic updates. A
`<title>`-based fallback (LinkedIn job page titles are typically
`"<Title> hiring at <Company> | LinkedIn"`) covers the most common breakage.

## Known limitation: `host_permissions` must list the API origin

`manifest.json`'s `host_permissions` currently covers `linkedin.com` and `http://localhost/*`
(any local port). If you point the extension's Settings → API base URL at a non-localhost API
(a real deployment), add that origin to `host_permissions` too — a Manifest V3 extension page's
`fetch()` is only exempt from CORS for origins explicitly listed there; an unlisted origin gets
blocked the same as an ordinary web page's cross-origin fetch would be (found via manual testing —
see DECISIONS.md Sprint 9).

## Not in this sprint

- Publishing to the Chrome Web Store (DEVELOPMENT_PLAN.md Sprint 9 explicitly scopes this out —
  a separate, later step once the extension has been used and iterated on).
- Employment type is not scraped (LinkedIn's job header doesn't expose it directly) — created
  applications default to `FullTime`, the same known limitation as generic CSV import
  (DECISIONS.md Sprint 4).
