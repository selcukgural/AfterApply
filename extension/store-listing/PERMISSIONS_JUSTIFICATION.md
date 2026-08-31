# Privacy practices tab — permissions justification

The Developer Dashboard's **Privacy practices** tab asks for a single-purpose description and a
written justification for every permission in `manifest.json`. Paste these in verbatim (trim if
the field has a character limit) — copy them as text since the field doesn't accept Markdown.

## Single purpose

```
This extension helps a signed-in e-kariyerim user get their job applications into their own
e-kariyerim account: with one click while viewing a LinkedIn or kariyer.net job posting, and by
walking them through forwarding relevant emails (interview invites, rejections, status updates)
to a personal e-kariyerim address so they can review and approve suggested status updates. Both
are the same single purpose — getting the user's own application activity into their own account —
via two entry points.
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
one-time script in that tab that reads the job title, company, and location already visible on the
page, so the user doesn't have to retype them into the popup. Nothing runs until that click.
```

**host_permissions — https://www.linkedin.com/*, https://www.kariyer.net/***
```
Required for the activeTab + scripting read above to run on these two job sites, and (LinkedIn
only) so the popup can detect a job opened via the search-results side panel.
```

**host_permissions — the e-kariyerim API origins (https://api.ekariyerim.com/*, the Cloud Run
origin)**
```
The extension submits the tracked application (title, company, location, job URL, and the scraped
description) to the user's own e-kariyerim account at this origin, authenticated with their
personal access token, and looks up existing company names for the autocomplete field. The same
origin is also used by the email-forwarding setup guide to fetch and display the user's personal
forwarding address and any pending Gmail confirmation code — still just the user's own account,
same token. No other network destination is contacted; the guide's links to Gmail's own settings
pages are plain outbound navigation, not a network request this extension makes, so they need no
host_permissions entry.
```

## Data usage disclosure (the form's checkbox section)

Chrome's form asks what data the item handles and how. Based on what `popup.js` actually sends to
`POST /api/applications/from-extension` and reads from `GET /api/companies/search`:

| Data type | Collected? | Notes |
|---|---|---|
| Personally identifiable information | No | The extension does not collect the user's name, address, or similar. |
| Authentication information | Yes | The user's own e-kariyerim personal access token, entered by the user, stored locally, used only to authenticate the extension's own requests to their account. |
| Website content | Yes | Job title, company name, location, and job description text scraped from the page the user opened, sent to the user's own e-kariyerim account. |
| Location, financial, health, personal communications | No | — |

Certifications (all true for this extension):
- Does **not** sell or transfer user data to third parties, outside the approved use cases.
- Does **not** use or transfer user data for purposes unrelated to the item's single purpose.
- Does **not** use or transfer user data to determine creditworthiness or for lending purposes.
