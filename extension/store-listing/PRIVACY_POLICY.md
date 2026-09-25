# e-kariyerim Browser Extension — Privacy Policy

_This document is the **source** for the published page. Its live copy is the web app's
`/extension-privacy` route — `web/src/app/[locale]/(public)/extension-privacy/page.tsx` plus the
`extensionPrivacy` block in `web/messages/tr.json` / `en.json` — served at
`https://ekariyerim.com/tr/extension-privacy` (and `/en/…`), which is the URL that goes in the
Dashboard's Store listing → Privacy practices → Privacy policy field. **Edit both together:** a
change here that never reaches that page is a policy that doesn't exist as far as Chrome Web Store
review is concerned, and vice versa. The published page is bilingual; this file is the English
text._

**Last updated:** 25 September 2026

## What this extension is

The e-kariyerim Browser Extension ("the extension") is a companion to the e-kariyerim web
application (ekariyerim.com). It lets a signed-in e-kariyerim user save a job posting they are
viewing — on LinkedIn, on kariyer.net, or on any other job site they choose to allow it on — as a
tracked application in their own e-kariyerim account, and offers an opt-in "Gmail Scanning" feature
that checks an email you open in Gmail on your own device and only sends a short summary when it
looks job-related.

## What data the extension accesses, and when

The extension does nothing until you click its toolbar icon. When you do, and only if the active
tab is a job posting it is allowed to read, it reads:

- The job title, company name, and location visible on that page.
- The job description text and a formatted (bold/headings/lists) snapshot of it, visible on that
  page.
- The page's URL, to identify the job and detect duplicates.
- On a LinkedIn posting that publicly shows a hiring-team card: the name and public profile URL of
  the person who posted the job, offered as the contact for that application — so you know who to
  follow up with. Most postings don't show one (it is the poster's own choice), and nothing is
  filled in when they don't.

Every field is shown to you, editable, in the extension's popup before anything is sent anywhere —
including the contact, which you can change or clear before saving.

### Sites beyond LinkedIn and kariyer.net

LinkedIn and kariyer.net are the two sites the extension can read as installed. Anywhere else —
Greenhouse, Lever, Ashby, Workday, Workable, SmartRecruiters, a company's own careers page, a job
board in your country — the popup shows an **Allow** button first, and Chrome asks you to confirm
that one site before the extension can read anything there. Nothing is read on a site you have not
allowed, and allowing one site says nothing about any other.

On those sites the extension reads the page's own schema.org job-posting markup: the structured
description of the job that the page already publishes for search engines. If the page has none,
nothing is filled in and you can still type the two fields yourself.

Every site you have allowed is listed in the extension's Settings page with a **Remove** button.
Removing it stops the extension reading that site, immediately.

### When the job posting is read a second time, on our servers

For a posting hosted on one of the applicant tracking systems above, e-kariyerim's own servers may
afterwards request that posting from that system's public, unauthenticated job-board API, to fill
in a description the page read could not reach — the text the CV-matching features need to work.
The request contains the posting's own public address and nothing about you: no account, no
cookie, no identifier. It happens on our servers after the application is saved, never in your
browser, and only for these systems:

- Greenhouse (`boards-api.greenhouse.io`)
- Lever (`api.lever.co`)
- Ashby (`api.ashbyhq.com`)
- SmartRecruiters (`api.smartrecruiters.com`)
- Workday (the employer's own `myworkdayjobs.com` / `myworkdaysite.com` address)

### Gmail Scanning (opt-in)

Gmail Scanning turns status-update emails (interview invites, rejections, status updates) into
suggestions without ever relaying your inbox anywhere. **It is off by default.** You turn it on
yourself, per device, from the extension's Settings page.

While off, nothing changes: the extension reads nothing on mail.google.com.

Once turned on, whenever you personally open an email in Gmail, the extension reads that one
message — sender address, subject line, and body text (capped at 2000 characters) — directly from
the page, in your own browser. It never reads your inbox list, and never reads a message you
haven't opened. That text is scored **entirely on your device**, against a small table of
job-application-related keywords and known job-site/ATS domains (downloaded from e-kariyerim and
cached locally — this table contains no personal data, only generic vocabulary and domain names).

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
- The access token your e-kariyerim account issued to this extension, and the date it expires (so
  the extension can tell you before the connection lapses instead of simply stopping). This token
  authenticates the extension's requests as you, and reaches only the endpoints the extension
  itself uses — it cannot export your application history or manage your account. You obtain it by
  pressing "Connect" and confirming a code on your own account page; if you use a custom API
  address you can still generate one in e-kariyerim's Settings and enter it by hand.
- Your light/dark theme preference for the extension's own popup.
- Your language choice (Turkish/English) for the extension's pages.
- Whether you've turned on Gmail Scanning (off unless you explicitly enable it), a cached copy of
  the (non-personal) keyword/domain table it scores against, and a short list of email thread IDs
  already submitted, so the same email isn't sent twice.
- Which version's "What's new" notes you have seen, and the version number of an update Chrome has
  downloaded but not applied yet — so the popup can show what changed after an update and offer a
  restart. Nothing about this is sent anywhere.

This data never leaves your device except as described in "What data the extension sends" below.

## What data the extension sends, and to whom

When you press "Connect," the extension asks the e-kariyerim API for a pairing code and opens the
confirmation page in a tab; while it waits it asks, every few seconds, whether you have confirmed
that code. Neither request carries any personal data — the extension has no account and no
credential at that point. The only thing that comes back is the access token, once, after you
confirm. If you refuse on that page, or the code runs out of time, nothing is issued at all.

When you click "I Applied" or "Apply later," the extension sends the job title, company, location,
job URL, description and contact details shown in the popup to the e-kariyerim API, authenticated
with your personal access token, so it can be saved to **your own e-kariyerim account** — as an
application, or as a posting saved to apply to later. A contact saved this
way is your own note of who to approach about that application: only you can see it, it is never
shown to other users or included in any analytics, e-kariyerim never emails or messages that person
on your behalf, and it is deleted along with the application (or saved posting) or your account. Company-name autocomplete similarly
queries the e-kariyerim API with the text you've typed. If
you've turned on Gmail Scanning, an opened email that scores as job-related sends its sender,
subject, and a capped snippet to your own e-kariyerim account (see the Gmail Scanning section
above) — this only happens for emails you open, only after you enable the setting, and only when
the on-device score qualifies.

The extension sends data to no other destination. It does not use analytics, advertising, or
tracking services, and it does not sell or share your data with third parties.

## Your controls

- The job-tracking popup only acts when you click its icon — there is no background scraping or
  polling on any site, including the ones you allow.
- A site you allowed can be taken back at any time from the extension's Settings page, which lists
  every one of them, or from Chrome's own extension settings.
- Gmail Scanning is off by default and does nothing until you turn it on in Settings; once on, it
  only reads an email when you personally open it in Gmail — it does not scan your inbox in the
  background, and does nothing at all on any other site. Turn it off anytime in Settings, with the
  same immediate effect.
- You can remove your access token at any time from the extension's Settings page, or revoke it
  from e-kariyerim's Settings → Browser Extension page, which immediately invalidates it. It also
  expires on its own after 90 days, whether or not you do anything.
- A pairing code is only ever an offer: it grants nothing until you confirm it while signed in, and
  the confirmation page has a "this wasn't me" button for a code you did not produce yourself.
- Uninstalling the extension deletes everything `chrome.storage.local` held for it (the token, API
  address, theme, language preference, Gmail Scanning setting, and its local caches) from your
  device.
- Data already saved to your e-kariyerim account (past applications) is governed by e-kariyerim's
  own privacy policy at ekariyerim.com, not this document — this page covers only the extension
  itself.

## Contact

Questions about this extension can be sent to the support address listed on its Chrome Web Store
listing page.
