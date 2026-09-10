# Privacy practices tab — permissions justification

The Developer Dashboard's **Privacy practices** tab asks for a single-purpose description and a
written justification for every permission in `manifest.json`. Paste these in verbatim (trim if
the field has a character limit) — copy them as text since the field doesn't accept Markdown.

## Single purpose

```
This extension helps a signed-in e-kariyerim user get their job applications into their own
e-kariyerim account: with one click while viewing a LinkedIn or kariyer.net job posting; or, if
they opt in, by locally checking an email they open in Gmail and sending only a short extracted
summary when it looks job-related. Both are the same single purpose — getting the user's own
application activity into their own account — via two entry points, the second capturing status
updates that arrive by email.
```

## Permission justifications

**storage**
```
Stores the user's e-kariyerim API base URL, the access token their account issued to this
extension (with the date it expires, so the extension can warn before it lapses), and their
light/dark theme and language preferences locally on the device (chrome.storage.local), so they
aren't re-entered on every use. Never synced, never sent anywhere except as this extension's own
Authorization header.
```

**activeTab**
```
Used only when the user clicks the extension's toolbar icon, to read the URL of the active tab and
determine whether it's a supported LinkedIn or kariyer.net job posting.
```

**scripting**
```
Used only after the user clicks the extension's toolbar icon on a supported job posting, to run a
one-time script in that tab that reads what is already visible on the page — the job title,
company, location and description, and, on a LinkedIn posting that shows a hiring-team card, the
name and profile URL of the person who posted the job — so the user doesn't have to retype them
into the popup. Everything read is shown in the popup and can be edited or cleared before it is
sent. Nothing runs until that click.
```

**host_permissions — https://www.linkedin.com/*, https://www.kariyer.net/***
```
Required for the activeTab + scripting read above to run on these two job sites, and (LinkedIn
only) so the popup can detect a job opened via the search-results side panel.
```

**host_permissions — https://mail.google.com/*, plus a declared content_scripts entry matching
the same origin (gmail-scan.js, local-filter-config.js)**
```
Powers the optional "Gmail Scanning" feature (off by default — see the single-purpose description
above), the extension's only content script declared directly in the manifest rather than injected
on a click, because Gmail is a single-page app with no full page reload between the inbox and an
opened email, so a one-time click-triggered read (like the LinkedIn/kariyer.net flow above) cannot
detect "the user just opened a different email." The script's very first action, before reading
anything, is checking a per-user setting (chrome.storage.local) the user must explicitly turn on
in the extension's own Settings page; while off, the script returns immediately and reads nothing.
Once on, it reads only the email currently open/expanded in the tab — sender, subject, and body
text of that single message, never the inbox list, never any message the user hasn't opened — scores
it locally in the browser against a small keyword/domain table, and only if that local score
suggests the email is job-application-related does it send an extracted summary (sender, subject,
and a capped snippet — never the full email body) to the user's own e-kariyerim account. An email
that doesn't look job-related, or any email while the setting is off, never leaves the browser.
The script does not make that request itself and never holds the user's access token: it hands the
extracted summary to the extension's own background service worker, which is what contacts the
account. Nothing about the destination or the data changes; it is the same request from a different
part of the same extension.
```

**host_permissions — the e-kariyerim API origins (https://api.ekariyerim.com/*, the Cloud Run
origin)**
```
The extension submits the tracked application (title, company, location, job URL, the scraped
description, and — when the posting showed one and the user left it in the popup — the job poster's
name and LinkedIn profile URL as the application's contact) to the user's own e-kariyerim account
at this origin, authenticated with their personal access token, and looks up existing company names
for the autocomplete field. The same
origin is also used — only when Gmail Scanning is turned on — by the extension's background service
worker, on the Gmail script's behalf, to submit an extracted email summary for a message that scored
as job-related and to fetch the (non-personal) keyword/domain table that scoring uses, so it can be
tuned without a new extension release. The same origin also serves the connection handshake: when the user presses
Connect, the extension asks this origin for a short pairing code, opens the account's confirmation
page in a tab, and asks (with a random secret only this extension holds) whether the user has
confirmed it — no personal data is sent in either request, and the access token is the answer to
the last one. All of it is still just the user's own account, same token. No other network
destination is contacted.
```

## `background` has no field — do not go looking for one

The Dashboard asks for a justification per **permission**: the entries under `permissions` and
`host_permissions`, and nothing else. `background` is neither — it is a manifest key describing how
the extension is built, like `options_page` or `icons`, so there is no box to paste anything into
and none is expected. Every block above corresponds to a real field; this section deliberately
does not.

It is written down because `background.js` is new in `0.8.0` and someone doing the upload will
notice it in the manifest and wonder. What it does, for the record: it makes the API calls on the
Gmail content script's behalf and holds the access token so that script never has to. No schedule,
no alarm, no page — it wakes to answer a request from that script, calls a fixed two-address
allow-list on the user's own account (submit an extracted email summary; fetch the non-personal
keyword table used for local scoring), and goes back to sleep. It reads nothing from any page and
handles no data the entries above don't already cover.

**The one Dashboard question it does touch is "Are you using remote code?" — and the answer stays
"No".** The worker fetches the scoring keyword table over the network, which sounds adjacent, but
remote code means script that is executed: a `<script>` from a remote URL, an eval'd string, a
module pulled at runtime. That table is JSON data, parsed and compared against text; every line of
executable code ships inside the package. Answering "Yes" here invites a review process the
extension does not need.

## Data usage disclosure (the form's checkbox section)

Chrome's form asks what data the item handles and how. Based on what `popup.js` actually sends to
`POST /api/applications/from-extension` and reads from `GET /api/companies/search`:

| Data type | Collected? | Notes |
|---|---|---|
| Personally identifiable information | Yes | Not about the user: the extension does not collect their name, address, or similar. But on a LinkedIn posting that publicly shows a hiring-team card, the popup reads the job poster's name and public profile URL and offers them as the application's contact — visible and editable in the popup before anything is sent, stored only on the user's own account as their own note of who to contact, never shown to anyone else and never used to contact that person. |
| Authentication information | Yes | The user's own e-kariyerim access token, stored locally, used only to authenticate the extension's own requests to their account. Obtained by the user pressing Connect and confirming a code on their own account page (or, for a custom API address, entered by hand); the extension holds a random secret for the duration of that handshake and nothing else. |
| Website content | Yes | Job title, company name, location, and job description text scraped from the LinkedIn/kariyer.net page the user opened, sent to the user's own e-kariyerim account. |
| Personal communications | Yes, opt-in only | Only if the user turns on Gmail Scanning in Settings (off by default): the sender, subject, and body text of an email the user personally opens in Gmail are read in the browser to score local relevance; only a short extracted summary (sender, subject, capped snippet — never the full email) is sent, and only for a message that scores as job-application-related, to the user's own e-kariyerim account. No other message is read or sent. |
| Location, financial, health | No | — |

Certifications (all true for this extension):
- Does **not** sell or transfer user data to third parties, outside the approved use cases.
- Does **not** use or transfer user data for purposes unrelated to the item's single purpose.
- Does **not** use or transfer user data to determine creditworthiness or for lending purposes.
