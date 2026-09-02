# Publishing to the Chrome Web Store — checklist

## Before you start

- [ ] **Publish (or re-publish) the privacy policy.** Put `PRIVACY_POLICY.md`'s content on a real
      page (e.g. `ekariyerim.com/privacy`) — the Dashboard rejects a submission with a
      token-storing extension and no privacy policy URL. **If this page was already published
      before the Gmail Scanning feature was added, its content is now stale** — the file gained a
      new "Gmail Scanning" section and updated "what's stored/sent"/"your controls" wording;
      re-publish the current file's content before submitting this version. This is the one manual
      step actually blocking submission right now — everything else on this checklist is either
      already done or is a Dashboard action.
- [x] **Production host permissions/API URL** — done. `manifest.json`'s `host_permissions` no
      longer lists `http://localhost/*`, and `DEFAULT_API_BASE_URL` in `storage.js` is
      `https://api.ekariyerim.com`.
- [ ] **One-time $5 developer registration fee**, if you haven't published anything from this
      Google account before: https://chrome.google.com/webstore/devconsole (Chrome asks for this on
      first use of the Dashboard).
- [x] **`manifest.json`'s `"version"`** is `0.5.0`, bumped from `0.4.0` for the Gmail Scanning
      feature (new `https://mail.google.com/*` host permission + a declared `content_scripts`
      entry — see `PERMISSIONS_JUSTIFICATION.md`'s new section on it). Bump it again for every
      subsequent upload — the Dashboard rejects a re-upload with a version already used.
- [ ] **Re-check the Privacy practices tab, not just the listing text.** This version added a new
      `host_permissions` entry and the extension's first declarative `content_scripts` entry
      (previously everything was click-triggered `scripting.executeScript`) — re-paste the updated
      single-purpose description, the new `https://mail.google.com/*` justification, and the
      updated data-usage table (a new "Personal communications: Yes, opt-in only" row) from
      `PERMISSIONS_JUSTIFICATION.md`. A permission whose justification doesn't match its actual use
      is one of the most common rejection reasons — see "After submitting" below — and this is the
      biggest permission change since the extension was first drafted for submission.

## Package the extension

From the repo root:

```bash
cd extension
zip -r ../e-kariyerim-extension.zip . -x "store-listing/*" -x "*.DS_Store"
```

This zips exactly what Chrome loads (`manifest.json`, the HTML/CSS/JS, `icons/`) and excludes the
`store-listing/` docs folder, which isn't part of the extension package.

## Upload

1. Go to the [Chrome Web Store Developer Dashboard](https://chrome.google.com/webstore/devconsole).
2. **New item** → upload `e-kariyerim-extension.zip`.
3. **Store listing** tab: fill in the fields from `LISTING.md` (name, summary, description,
   category, language — add the Turkish translation too).
4. **Graphic assets**: upload the PNGs from `screenshots/` (1280×800 each). At least one is
   required; up to five are shown.
5. **Privacy practices** tab: paste in the single-purpose description and each permission
   justification from `PERMISSIONS_JUSTIFICATION.md`, fill in the data-usage checkboxes as listed
   there, and paste the published privacy policy URL.
6. **Distribution** tab: choose visibility (Public, or Unlisted/Private if you want to test with a
   small group first — Unlisted is a good first step before going Public) and the countries where
   it should be available.
7. Save, then **Submit for review**.

## After submitting

- Review typically takes from a few hours to a few days; extensions requesting broad host
  permissions or handling auth tokens sometimes take longer or get follow-up questions — check the
  Dashboard's email notifications.
- If rejected, the Dashboard states the specific policy violation. The most common ones for an
  extension like this are: missing/inaccessible privacy policy URL, a permission whose
  justification doesn't clearly match its actual use, or a "single purpose" description that reads
  as multiple unrelated features (not currently a risk here — the extension does one thing).
- Once approved, note the item's Web Store URL in `extension/README.md` "Not in this sprint" (that
  section currently records that publishing was explicitly out of scope for Sprint 9 — update it
  once this ships).

## Updating a published extension later

1. Bump `"version"` in `manifest.json`.
2. Re-zip (same command as above).
3. Dashboard → your item → **Package** → upload the new zip → Submit for review. Store listing
   text/screenshots don't need to be re-submitted unless you're changing them too.
