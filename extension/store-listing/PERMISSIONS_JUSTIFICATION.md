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
Stores the user's e-kariyerim API base URL, personal access token, and light/dark theme
preference locally on the device (chrome.storage.local), so they aren't re-entered on every use.
Never synced, never sent anywhere except as this extension's own Authorization header.
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
```

**host_permissions — the e-kariyerim API origins (https://api.ekariyerim.com/*, the Cloud Run
origin)**
```
The extension submits the tracked application (title, company, location, job URL, the scraped
description, and — when the posting showed one and the user left it in the popup — the job poster's
name and LinkedIn profile URL as the application's contact) to the user's own e-kariyerim account
at this origin, authenticated with their personal access token, and looks up existing company names
for the autocomplete field. The same
origin is also used — only when Gmail Scanning is turned on — by gmail-scan.js to submit an
extracted email summary for a message that scored as job-related, and by local-filter-config.js to
fetch the (non-personal) keyword/domain table that scoring uses, so it can be tuned without a new
extension release. All of it is still just the user's own account, same token. No other network
destination is contacted.
```

## Data usage disclosure (the form's checkbox section)

Chrome's form asks what data the item handles and how. Based on what `popup.js` actually sends to
`POST /api/applications/from-extension` and reads from `GET /api/companies/search`:

| Data type | Collected? | Notes |
|---|---|---|
| Personally identifiable information | Yes | Not about the user: the extension does not collect their name, address, or similar. But on a LinkedIn posting that publicly shows a hiring-team card, the popup reads the job poster's name and public profile URL and offers them as the application's contact — visible and editable in the popup before anything is sent, stored only on the user's own account as their own note of who to contact, never shown to anyone else and never used to contact that person. |
| Authentication information | Yes | The user's own e-kariyerim personal access token, entered by the user, stored locally, used only to authenticate the extension's own requests to their account. |
| Website content | Yes | Job title, company name, location, and job description text scraped from the LinkedIn/kariyer.net page the user opened, sent to the user's own e-kariyerim account. |
| Personal communications | Yes, opt-in only | Only if the user turns on Gmail Scanning in Settings (off by default): the sender, subject, and body text of an email the user personally opens in Gmail are read in the browser to score local relevance; only a short extracted summary (sender, subject, capped snippet — never the full email) is sent, and only for a message that scores as job-application-related, to the user's own e-kariyerim account. No other message is read or sent. |
| Location, financial, health | No | — |

Certifications (all true for this extension):
- Does **not** sell or transfer user data to third parties, outside the approved use cases.
- Does **not** use or transfer user data for purposes unrelated to the item's single purpose.
- Does **not** use or transfer user data to determine creditworthiness or for lending purposes.
