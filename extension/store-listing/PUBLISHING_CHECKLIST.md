# Publishing to the Chrome Web Store — checklist

**The item is already live; this is an update.** `0.6.0` is the version currently published to
the Chrome Web Store (confirmed with the maintainer 2026-09-07), at
<https://chromewebstore.google.com/detail/e-kariyerim-%E2%80%94-job-import/lemdkeljacdgbbcmefpbggphnhhciimi>.
Gmail Scanning, the `mail.google.com` host permission, the content script and HR contact
capture are therefore all live to real users. `0.4.0` was the first publish, around 2026-09-01
(commit `fa2daff` added the install link to the help centre "now that the extension is
published"). Always confirm the currently published version in the Dashboard before uploading;
the repo can only show what was committed, not what was shipped.
Current package version: **0.7.0** — uploaded to the Dashboard on 2026-09-09 and **awaiting
review**. Until it is approved, `0.6.0` is what users are running: the Connect flow exists in the
API and at `/pair` but reaches nobody through the store yet, and pasted keys keep working
regardless (`/from-extension` is unchanged).

## Before you start

- [x] **The extension privacy policy page is deployed and current** — done. `/extension-privacy`
      is live in both locales; <https://ekariyerim.com/tr/extension-privacy> was fetched and
      checked on 2026-09-09 and carries Gmail Scanning, the HR contact and the stored token expiry
      date, dated "8 Eylül 2026" — the same date as `PRIVACY_POLICY.md` in this folder, which is
      its source text. The two are in step; keep them that way.
- [ ] **Point the Dashboard at that URL — first thing on the next upload.** Paste
      `https://ekariyerim.com/tr/extension-privacy` into **Store listing → Privacy practices →
      Privacy policy**. Whatever URL is in that field today was entered for `0.4.0` and predates
      Gmail Scanning and the HR contact. Note `ekariyerim.com/privacy` is a *different* document —
      the account-level policy — and the two link to each other.
      **Why it is the first thing and not a tidy-up:** the extension has no privacy link of its
      own — not in `options.html`, not in `popup.html`, nowhere — so this Dashboard field is the
      only route a user of the published item has to the policy at all.
      **Deliberately not done during the `0.7.0` review** (2026-09-09): the Dashboard would not let
      these fields be edited without cancelling the pending review, and `0.7.0` adds no new
      permission over the already-approved `0.6.0`, so the trade was a certain delay against a
      speculative gain. See `DECISIONS.md` 2026-09-09. If `0.7.0` is rejected, do this as part of
      the re-submit; if it is approved, it goes with `0.8.0`.
      **This step cannot be automated:** Chrome blocks all extension scripting on the Web Store
      domains, the Developer Console included ("The extensions gallery cannot be scripted"), so no
      browser-automation tool can reach these fields. A human has to type them.
- [ ] **Add a privacy-policy link to the extension itself** (`options.html`, pointing at
      `https://ekariyerim.com/{tr,en}/extension-privacy`). Found missing 2026-09-09. This is a code
      change, so it rides on a release that bumps the version anyway.
- [x] **Production host permissions/API URL** — done. `manifest.json`'s `host_permissions` no
      longer lists `http://localhost/*`, and `DEFAULT_API_BASE_URL` in `storage.js` is
      `https://api.ekariyerim.com`.
- [x] **One-time $5 developer registration fee** — paid. It was a precondition of the first
      publish, and this item has been live since `0.4.0`; the box was simply never ticked.
- [x] **`manifest.json`'s `"version"`** is `0.7.0`. Bump it for every subsequent upload — the
      Dashboard rejects a re-upload with a version already used. The popup and Settings footers
      render this same number (`version.js` reads it off the manifest), so it is also what a bug
      report will quote back at you.
- [x] **Screenshots reshot for 0.7.0** — done 2026-09-08. The Settings page lost its
      paste-a-token-here layout and gained the Connect button, the connection state line and the
      "advanced" disclosure, which stales `options-light.png` and the help centre's
      `chrome-extension-options.png`; `scene-options.html` was rewritten first, since its markup is
      copied rather than shared. The web app's own `settings-extension-token.png` was reshot too —
      that section lost its paste-a-key layout as well. The popup's job form is unchanged, so `popup-light.png` /
      `popup-dark.png` still show what ships.
- [x] **Screenshots reshot for 0.6.0** — done 2026-09-06. The previous set predated the popup's
      three HR-contact fields and both pages' installed-version footer, and the `scene-*.html`
      compositions carry copied markup so they didn't pick either up on their own; both were
      updated, then all three reshot at 1280×800. The help centre's
      `web/public/help/screenshots/chrome-extension-{popup,options}.png` were stale for the same
      reason and were reshot too. `screenshots/README.md` documents both recipes.
- [ ] **Fill in the Privacy practices tab from `PERMISSIONS_JUSTIFICATION.md`** — the
      single-purpose description, one justification per permission, and the data-usage table. A
      permission whose justification doesn't match its actual use is one of the most common
      rejection reasons (see "After submitting"), and this extension asks for a lot: two job sites,
      `https://mail.google.com/*`, and a declared `content_scripts` entry for Gmail Scanning.

## What changed since 0.6.0

- **One-click connection (`0.7.0`).** The extension no longer asks anyone to paste a key. Press
  Connect in its Settings and it asks the API for a short pairing code, opens
  `ekariyerim.com/{tr,en}/pair?code=…` in a tab, and collects the token once the user confirms
  there — signing up on that page if they have no account yet. What this changes for the Dashboard:
  the **storage** justification and the data-usage table's *Authentication information* row now
  describe a token the account issues to the extension rather than one the user pastes in, and
  `LISTING.md` gained a SETUP/KURULUM paragraph in both languages. **No new permissions**:
  `chrome.tabs.create` needs none, and the pairing endpoints live on the API origin the extension
  already reaches.
- **The connection stops dying silently (`0.7.0`).** The token still expires after 90 days, but the
  extension now stores the expiry with it: the popup and Settings warn in the last two weeks, an
  expired connection says so instead of showing "could not reach e-kariyerim", and a 401 on submit
  is reported as an expired connection rather than a network error. `PRIVACY_POLICY.md`'s "what the
  extension stores" list gained the expiry date, and the published `/extension-privacy` page was
  updated with it.
- **Entering a key by hand still works**, under an "advanced" disclosure in both the extension's
  Settings and the web app's — every already-installed build keeps working untouched.

## What changed since 0.5.0

Useful when filling in the Dashboard, and as the diff to re-check before uploading:

- **HR contact capture (`0.6.0`).** On a LinkedIn posting that renders a hiring-team card, the
  popup now also reads the job poster's **name and LinkedIn profile URL** and submits them with the
  application. This is the first third-party personal data the extension touches, so it changed the
  data-usage table's "Personally identifiable information" row to **Yes**, and added a paragraph to
  `PRIVACY_POLICY.md` and a sentence to both language versions of `LISTING.md` — make sure the
  Dashboard reflects that, not the older "No".
- **Email forwarding removed.** The `email-forwarding.*` pages are gone and Gmail Scanning is the
  only email path, so `manifest.json`'s description, `LISTING.md`, `PRIVACY_POLICY.md` and the
  screenshots were all rewritten. The two `forwarding-*.png` assets were deleted.
- **Installed-version footer (`0.6.0`).** The popup and Settings now render the manifest version.
  Its `.version-line` rule initially had no horizontal padding — the footer is a sibling of
  `<main>`, not a child, so it inherited none of `main#content`'s inset and the right-aligned text
  sat flush against the popup's edge; fixed while reshooting.
- **Permissions themselves are unchanged since 0.5.0** — same `permissions`, `host_permissions` and
  `content_scripts`. Only the wording around them moved.

## Package the extension

From the repo root:

```bash
rm -f e-kariyerim-extension.zip
cd extension
zip -r ../e-kariyerim-extension.zip . -x "store-listing/*" -x "README.md" -x "*.DS_Store"
```

This zips exactly what Chrome loads (`manifest.json`, the HTML/CSS/JS, `icons/`). Two exclusions,
both developer-only material that would otherwise be published to anyone who unpacks the item:
`store-listing/` (these docs) and `README.md`, which is internal notes — sprint history, local-dev
instructions, references to `DECISIONS.md` and the planning docs. The `rm -f` matters too: `zip`
*adds to* an existing archive rather than replacing it, so without it a stale build's files ride
along. The archive is gitignored.

Verify before uploading — `unzip -l ../e-kariyerim-extension.zip` should list exactly the
manifest, the four `.html`/`.css` files, the `.js` modules, and `icons/`, and nothing else.

## Upload

1. Go to the [Chrome Web Store Developer Dashboard](https://chrome.google.com/webstore/devconsole).
2. **New item** → upload `e-kariyerim-extension.zip`.
3. **Store listing** tab: fill in the fields from `LISTING.md` (name, summary, description,
   category, language — add the Turkish translation too).
4. **Graphic assets**: upload the three PNGs from `screenshots/` (`popup-light.png`,
   `popup-dark.png`, `options-light.png`, 1280×800 each). At least one is required; up to five are
   shown.
5. **Privacy practices** tab: paste in the single-purpose description and each permission
   justification from `PERMISSIONS_JUSTIFICATION.md`, fill in the data-usage checkboxes as listed
   there, and paste the published privacy policy URL from the first item above.
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
- Once approved, check that the version shown on the listing is the one you uploaded — that is the
  only external confirmation that the update actually went live, and it is what
  `version.js` will start reporting in the popup as users update.

## Updating a published extension later

Also the routine for *this* upload, since the item is already live.

1. Bump `"version"` in `manifest.json`.
2. Update whatever this folder's docs the change invalidates — `PERMISSIONS_JUSTIFICATION.md` if
   `permissions`/`host_permissions`/`content_scripts` or the data the extension sends changed,
   `PRIVACY_POLICY.md` if what's stored or sent changed, `LISTING.md` for user-facing copy — and
   reshoot `screenshots/` if the popup or Settings page looks different.
3. Re-zip (same command as above).
4. Dashboard → your item → **Package** → upload the new zip → Submit for review. Store listing
   text/screenshots don't need to be re-submitted unless you're changing them too; a changed
   privacy policy or permission set does need the Privacy practices tab re-checked.
5. If the policy text changed, deploy `/extension-privacy` **before** submitting — the reviewer
   fetches that URL live.
