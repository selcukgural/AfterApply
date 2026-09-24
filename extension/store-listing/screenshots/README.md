# Screenshot sources

- `popup-light.png` / `popup-dark.png` — the popup auto-filling a LinkedIn job, light and dark.
- `options-light.png` — the Settings page.

All three are 1280×800 PNGs (the Chrome Web Store's required screenshot size) generated from the
`scene-*.html` files in this folder, which are marketing compositions, not the shipped extension
pages — they reuse `../../popup.css`'s real classes directly (`.app-header`, `.site-badge`,
labels/inputs/buttons, `.theme-toggle`) and `../../icons/icon48.png` for the app mark, so they
can't visually drift from the real pages, but they render outside a real browser-extension
context. Neither this folder nor `store-listing/` is part of the packaged extension (see
`PUBLISHING_CHECKLIST.md`'s zip command).

They only mostly can't drift: the classes and the icon are shared, but the *markup* is copied.
When the app mark became a raster asset, `popup.html` moved to `<img src="icons/icon48.png">`
while these scenes kept their own inline chat-bubble `<svg>`, so the store screenshots went on
showing the old logo. Prefer a shared asset over a copied glyph here.

## Regenerating them

Last shot 2026-09-22 for `0.9.0` — `options-light.png` only.

The help centre's two extension images (below) were reshot 2026-09-24 when `0.9.1` went live in
the Store: both footers still read an old build — Settings `0.9.0`, the popup `0.6.0`. Windows
1280×1052 (Settings, which grew by the allowed-sites list) and 1280×870 (popup).

The two popup shots were reshot once, on an ATS posting with the provenance badge, and then put
back. `0.9.0` was rejected for keyword spam over the six ATS names in its description
(PUBLISHING_CHECKLIST.md), and the rule that came out of it covers screenshots too: Store metadata
does not carry third-party brands. A shot of the popup over `jobs.lever.co` is a third-party brand
in metadata, so the scene stays on the LinkedIn posting — which is also still accurate, since
`buildForm()` renders the provenance badge only for `foundBy === "jsonld"`/`"meta"` and LinkedIn
reports `"linkedin"`. The site's own landing mock follows the scene because a contract test pins
the pair; the site's *text* is free to name the systems, and does.

**The one reshoot did catch a real styling bug**, which is why it was worth doing anyway:
`buildForm()` renders the provenance note as `<span class="site-badge muted-badge">` and
`muted-badge` did not exist in `popup.css`, so the note rendered in the accent pill, identical to
the site badge beside it — "Greenhouse · sayfadan okundu" read as two site names. The modifier is
in `popup.css` now. Reviewing the markup would not have shown it; rendering it did.

`scene-options.html` had to lose two rows to fit: the "Not connected yet." status and the "Enter a
key by hand" row. The canvas is a fixed 1280×800 and the card grew by a whole section; without that
trim the new section falls off the bottom edge, which is exactly what the first two attempts at
this shot produced.

**The shot caught a real styling bug before it shipped.** `popup.css`'s `button.secondary` is
`display: block; width: 100%; margin-top: 10px` — built for the full-width "open the page again"
button. Dropped into a list row it took the whole line and pushed the host name into a wrap. Both
`options.html` and this scene now override it to an inline chip. Reviewing the markup would not
have shown that; rendering it did.

Before that, 2026-09-10 for `0.8.0` — `options-light.png` only, for the privacy-policy link the
Settings footer gained (the release's other change, the background service worker, is invisible).
The two popup shots were left alone that time too: `popup.html` was untouched by that release, and
its footer deliberately still holds the version alone.

Each `scene-*.html` renders its own 1280×800 design inside a flex-centered viewport, and takes a
**`?pin=1`** parameter that drops the fit-to-viewport scale-down and pins the canvas to the
viewport's top-left corner — that used to be a block of CSS pasted into the console on every
reshoot. With it, headless Chrome alone is enough and no capture tool has to agree about
coordinates:

1. Serve `extension/` with any static file server — `python3 -m http.server 8803` run from the
   `extension/` directory.
2. Shoot it (macOS has no `timeout`, and Chrome's `--screenshot` does not always exit on its own,
   so run it in the background, wait for the file, then kill it):

   ```bash
   "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --disable-gpu \
     --hide-scrollbars --force-device-scale-factor=2 --window-size=1280,800 \
     --virtual-time-budget=4000 --user-data-dir=/tmp/shotprofile --screenshot=raw.png \
     "http://localhost:8803/store-listing/screenshots/scene-options.html?theme=light&pin=1" &
   ```

   Use a **fresh** `--user-data-dir` (or a cache-busting query param) after editing a scene: Chrome
   will otherwise serve the previous version from cache and hand you an identical-looking PNG,
   which is easy to mistake for "my edit did nothing".
3. Normalise: `sips -s format png -z 800 1280 raw.png --out options-light.png`.
4. Shrink. A `sips` PNG of a 1280×800 UI screenshot lands around 350–450 KB; quantising to a
   256-colour adaptive palette cuts that to ~100–125 KB with no visible loss, because these are
   flat UI surfaces plus text rather than photographs. It matters most for the two help-centre
   images, which real visitors download:

   ```python
   from PIL import Image
   im = Image.open(path).convert("RGB")
   im.quantize(colors=256, method=Image.Quantize.MEDIANCUT,
               dither=Image.Dither.FLOYDSTEINBERG).save(path, optimize=True)
   ```

   Check a dark shot afterwards — a dark gradient is where banding would show up first.

To change the sample content (job title/company, headline copy, theme), edit the `scene-*.html`
file directly and reshoot.

## The help centre's two extension images

`web/public/help/screenshots/chrome-extension-popup.png` and `chrome-extension-options.png` go
stale from the same commits these do, but they are **not** the marketing scenes — they are the
real `popup.html` / `options.html`, which can't be captured from an ordinary tab because they need
the `chrome.*` APIs. Recipe:

1. Copy `extension/` to a scratch directory, drop `store-listing/` (and, since `0.9.0`,
   `node_modules/`, `tests/`, `package*.json` and `vitest.config.js` — the test harness), and add a
   `chrome-stub.js` that defines `chrome.runtime.getManifest` (**including `host_permissions`**,
   which `options.js` reads to filter the allowed-sites list), `chrome.storage.local` (seeded with
   `afterapply_settings` holding a token, `afterapply_theme`, `afterapply_language: "tr"`,
   `afterapply_gmail_scan_enabled: true`), `chrome.permissions` (`getAll` returning a few granted
   origins plus the manifest ones, `contains`, `request`, `remove`, and `onAdded`/`onRemoved` with
   an `addListener` that does nothing — `options.js` registers both at module scope and throws
   without them), `chrome.tabs.query` returning a LinkedIn job URL, and
   `chrome.scripting.executeScript` returning a scraped-job object with a `foundBy` field. Load it with a plain
   `<script src="chrome-stub.js">` placed **before** each page's `<script type="module">`, so the
   stub exists by the time `popup.js` runs `main()`.
2. Add a `frame.html` that centres an `<iframe>` of the page on a soft gradient inside a
   1280-wide, top-left-pinned canvas, sizes the iframe to `contentDocument`'s height once it has
   rendered, and applies a fixed `scale` (1.35 reads well for both pages).
3. Serve the scratch copy, open `frame.html?src=popup.html&w=360&scale=1.35` (and
   `?src=options.html&w=420&scale=1.35`), then capture and downscale as in the steps above —
   these two are 1280 wide with a free height, since the help page renders them `w-full`. Set
   `--window-size` to roughly the rendered height (1280×760 fitted the 0.7.0 Settings page;
   1280×1005 fitted the 0.9.0 one), or the shot comes back with a band of empty gradient
   underneath. Cropping that band off afterwards does not work as neatly as it sounds — the
   frame's canvas gradient and the body background differ by a few units, so "find the last row
   that is not the background colour" finds the very last row. Get the window height right instead.

The stub is also the cheapest way to *look at* a page that only runs inside an extension. The
0.7.0 Settings page shipped its "open the confirmation page again" button visible with no pairing
in flight, because `popup.css`'s own `button { display: block }` outranks the UA stylesheet's
`[hidden] { display: none }` — invisible in review, obvious in the screenshot. `popup.css` now
carries an explicit `[hidden]` rule.

The scratch copy exists so the stub never ships: `chrome-stub.js` must not end up in `extension/`.

A third help image, `web/public/help/screenshots/settings-extension-token.png`, is the *web app's*
Settings page and goes stale from the same commits — it is shot from the seeded demo account
against the local stack, driving headless Chrome over CDP (sign in by POSTing `/api/auth/login`
from the page and writing `tokenStorage`'s localStorage keys, `Emulation.setDeviceMetricsOverride`
at 1280 wide / dsf 2, `Page.captureScreenshot` with `captureBeyondViewport`, then crop and
downscale to 1280 wide).
