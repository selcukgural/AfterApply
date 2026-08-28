# e-kariyerim Browser Extension — Privacy Policy

_Draft — publish this content at a real, publicly reachable URL (e.g. `ekariyerim.com/privacy` or
a dedicated `ekariyerim.com/extension-privacy` page) before submitting to the Chrome Web Store, and
enter that URL under Store listing → Privacy practices. Update the "Last updated" line when you do._

**Last updated:** _fill in on publish_

## What this extension is

The e-kariyerim Browser Extension ("the extension") is a companion to the e-kariyerim web
application (ekariyerim.com). It lets a signed-in e-kariyerim user save a job posting they are
viewing on LinkedIn or kariyer.net as a tracked application in their own e-kariyerim account.

## What data the extension accesses, and when

The extension does nothing until you click its toolbar icon. When you do, and only if the active
tab is a supported LinkedIn or kariyer.net job posting, it reads:

- The job title, company name, and location visible on that page.
- The job description text and a formatted (bold/headings/lists) snapshot of it, visible on that
  page.
- The page's URL, to identify the job and detect duplicates.

Every field is shown to you, editable, in the extension's popup before anything is sent anywhere.

## What data the extension stores

The extension stores the following locally on your device, using the browser's own
`chrome.storage.local` (never Chrome Sync, never a third-party server):

- The e-kariyerim API address you're using (a setting, not personal data).
- Your e-kariyerim personal access token, which you generate yourself from e-kariyerim's Settings
  page and paste in. This token authenticates the extension's requests as you.
- Your light/dark theme preference for the extension's own popup.

This data never leaves your device except as described in "What data the extension sends" below.

## What data the extension sends, and to whom

When you click "I Applied," the extension sends the job title, company, location, job URL, and
description shown in the popup to the e-kariyerim API, authenticated with your personal access
token, so it can be saved to **your own e-kariyerim account**. Company-name autocomplete similarly
queries the e-kariyerim API with the text you've typed.

The extension sends data to no other destination. It does not use analytics, advertising, or
tracking services, and it does not sell or share your data with third parties.

## Your controls

- The extension only acts when you click its icon — there is no background scraping or polling.
- You can remove your access token at any time from the extension's Settings page, or revoke it
  from e-kariyerim's Settings → Browser Extension page, which immediately invalidates it.
- Uninstalling the extension deletes everything `chrome.storage.local` held for it (the token,
  API address, and theme preference) from your device.
- Data already saved to your e-kariyerim account (past applications) is governed by e-kariyerim's
  own privacy policy at ekariyerim.com, not this document — this page covers only the extension
  itself.

## Contact

Questions about this extension can be sent to the support address listed on its Chrome Web Store
listing page.
