# e-kariyerim Browser Extension — Privacy Policy

_Draft — publish this content at a real, publicly reachable URL (e.g. `ekariyerim.com/privacy` or
a dedicated `ekariyerim.com/extension-privacy` page) before submitting to the Chrome Web Store, and
enter that URL under Store listing → Privacy practices. Update the "Last updated" line when you do._

**Last updated:** _fill in on publish_

## What this extension is

The e-kariyerim Browser Extension ("the extension") is a companion to the e-kariyerim web
application (ekariyerim.com). It lets a signed-in e-kariyerim user save a job posting they are
viewing on LinkedIn or kariyer.net as a tracked application in their own e-kariyerim account, and
offers two optional ways to turn status-update emails into suggestions: a setup guide for
forwarding those emails (e.g. from Gmail) to a personal e-kariyerim address, and an opt-in "Gmail
Scanning" feature that checks an email you open in Gmail on your own device and only sends a short
summary when it looks job-related.

## What data the extension accesses, and when

The extension does nothing until you click its toolbar icon. When you do, and only if the active
tab is a supported LinkedIn or kariyer.net job posting, it reads:

- The job title, company name, and location visible on that page.
- The job description text and a formatted (bold/headings/lists) snapshot of it, visible on that
  page.
- The page's URL, to identify the job and detect duplicates.

Every field is shown to you, editable, in the extension's popup before anything is sent anywhere.

The email-forwarding setup guide, opened from the extension's Settings page, reads your personal
e-kariyerim forwarding address and any pending Gmail forwarding-confirmation code from your own
e-kariyerim account (using the same personal access token). It never signs in to, connects to, or
reads your email account in any way — the actual forwarding is something you set up entirely
yourself, in your own Gmail settings, using Gmail's own built-in forwarding feature. The guide's
links to Gmail's own settings pages are ordinary outbound links that open in your browser; the
extension does not read or modify anything on those pages.

Once you turn on forwarding in Gmail, every incoming email is relayed to your personal
e-kariyerim address automatically — Gmail's built-in forwarding is all-or-nothing, it cannot be
scoped by sender. To keep that from meaning "e-kariyerim sees your whole inbox," our backend
automatically discards, without storing, any forwarded email that isn't from a well-known job
site/ATS platform or a company you've already added to e-kariyerim; only the subject line and a
short snippet of a recognized status-update email are stored, and only to show you a suggestion
you can approve or dismiss.

### Gmail Scanning (opt-in, beta) — an alternative to forwarding

Gmail Scanning is a different way to get status updates from email, for anyone who would rather
not relay their whole inbox anywhere. **It is off by default.** You turn it on yourself, per
device, from the extension's Settings page.

While off, nothing changes: the extension reads nothing on mail.google.com.

Once turned on, whenever you personally open an email in Gmail, the extension reads that one
message — sender address, subject line, and body text (capped at 2000 characters) — directly from
the page, in your own browser. It never reads your inbox list, and never reads a message you
haven't opened. That text is scored **entirely on your device**, against a small table of
job-application-related keywords and known job-site/ATS domains (the same table the
forwarding-mail backend uses, downloaded from e-kariyerim and cached locally — this table contains
no personal data, only generic vocabulary and domain names).

Only if that on-device score suggests the email is genuinely job-application-related does the
extension send anything: the sender address, subject line, and the same capped snippet — never the
full email body, and never anything about a message that didn't score as relevant — to your own
e-kariyerim account, to be turned into a suggestion you can review. An email that scores as
unrelated (which is most email — personal messages, receipts, newsletters, and so on) is never
sent anywhere and is discarded the moment scoring finishes.

The extension also keeps a small local list (on your device) of email threads it has already
submitted, so re-opening the same email doesn't send a duplicate — this list, and the keyword
table above, never leave your device except as the read-only fetch that downloads the table.

## What data the extension stores

The extension stores the following locally on your device, using the browser's own
`chrome.storage.local` (never Chrome Sync, never a third-party server):

- The e-kariyerim API address you're using (a setting, not personal data).
- Your e-kariyerim personal access token, which you generate yourself from e-kariyerim's Settings
  page and paste in. This token authenticates the extension's requests as you.
- Your light/dark theme preference for the extension's own popup.
- Your language choice (Turkish/English) for the email-forwarding setup guide only.
- Whether you've turned on Gmail Scanning (off unless you explicitly enable it), a cached copy of
  the (non-personal) keyword/domain table it scores against, and a short list of email thread IDs
  already submitted, so the same email isn't sent twice.

This data never leaves your device except as described in "What data the extension sends" below.

## What data the extension sends, and to whom

When you click "I Applied," the extension sends the job title, company, location, job URL, and
description shown in the popup to the e-kariyerim API, authenticated with your personal access
token, so it can be saved to **your own e-kariyerim account**. Company-name autocomplete similarly
queries the e-kariyerim API with the text you've typed. The email-forwarding guide sends your
personal access token to the same API to fetch your forwarding address/confirmation code, and
(only if you click "dismiss") a request to clear that confirmation code once you've used it. If
you've turned on Gmail Scanning, an opened email that scores as job-related sends its sender,
subject, and a capped snippet to your own e-kariyerim account (see the Gmail Scanning section
above) — this only happens for emails you open, only after you enable the setting, and only when
the on-device score qualifies.

The extension sends data to no other destination. It does not use analytics, advertising, or
tracking services, and it does not sell or share your data with third parties.

## Your controls

- The job-tracking popup only acts when you click its icon — there is no background scraping or
  polling on LinkedIn/kariyer.net.
- Gmail Scanning is off by default and does nothing until you turn it on in Settings; once on, it
  only reads an email when you personally open it in Gmail — it does not scan your inbox in the
  background, and does nothing at all on any other site. Turn it off anytime in Settings, with the
  same immediate effect.
- You can remove your access token at any time from the extension's Settings page, or revoke it
  from e-kariyerim's Settings → Browser Extension page, which immediately invalidates it.
- Uninstalling the extension deletes everything `chrome.storage.local` held for it (the token, API
  address, theme, language preference, Gmail Scanning setting, and its local caches) from your
  device.
- Setting up (or not setting up) email forwarding is entirely your choice, done in your own Gmail
  settings — you can stop it anytime by turning off forwarding there, independent of this
  extension.
- Data already saved to your e-kariyerim account (past applications) is governed by e-kariyerim's
  own privacy policy at ekariyerim.com, not this document — this page covers only the extension
  itself.

## Contact

Questions about this extension can be sent to the support address listed on its Chrome Web Store
listing page.
