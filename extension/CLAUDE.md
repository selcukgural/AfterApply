# Chrome extension release policy

A change under `extension/` is a release, not just a code edit. In the same change:

1. **Bump `"version"` in `extension/manifest.json`.** The Web Store rejects a re-upload of a
   version already used, and the popup/Settings footer renders this number (`version.js`), so it
   is what a bug report will quote back at you. The item is already live, so every upload is an
   update — see `extension/store-listing/PUBLISHING_CHECKLIST.md` for the state of the listing.
2. **Update the publish material the change invalidates**, under `extension/store-listing/`:
   `PERMISSIONS_JUSTIFICATION.md` when `permissions`/`host_permissions`/`content_scripts` or the
   data the extension sends changes (its data-usage table is what the Dashboard's Privacy
   practices tab gets), `PRIVACY_POLICY.md` when what is stored or sent changes — **and with it
   the published page it is the source for, `/extension-privacy` in the web app; the two must
   never drift** — `LISTING.md` for user-facing copy (both TR and EN), and `PUBLISHING_CHECKLIST.md`
   when the state of the listing moves.
3. **Reshoot the screenshots the change stales, before committing.** Extension UI changes reach
   two sets of public images: `extension/store-listing/screenshots/*.png` (Web Store assets, built
   from the `scene-*.html` compositions, which carry *copied* markup and so never update
   themselves) and `web/public/help/screenshots/chrome-extension-{popup,options}.png` (help
   centre, shot from the real pages). Recipes for both are in `screenshots/README.md`.
4. **Build the store package:**
   `rm -f e-kariyerim-extension.zip && cd extension && zip -r ../e-kariyerim-extension.zip . -x "store-listing/*" -x "README.md" -x "CLAUDE.md" -x "*.DS_Store" -x "package.json" -x "package-lock.json" -x "node_modules/*" -x "tests/*" -x "vitest.config.js"`

   Then look inside it (`unzip -l ../e-kariyerim-extension.zip`) before uploading. The list should
   be the runtime files and `icons/` and nothing else.

   The exclusions past `*.DS_Store` are the test harness added in 0.9.0 (`npm test` in this
   directory). None of it ships: the extension is still plain files with no build step, and
   uploading `node_modules` would both bloat the package and give the Web Store reviewer
   thousands of files of third-party code to read. `CLAUDE.md` — this file — was missing from the
   list until 0.9.0 and therefore shipped inside the 0.8.0 package: harmless to a user, but it is
   our own release checklist, and handing a reviewer a document about how we talk to the Store is
   not something to do by accident.
