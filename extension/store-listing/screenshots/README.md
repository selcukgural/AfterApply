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

Last shot 2026-09-06 for `0.6.0` — the popup's three HR-contact fields and both pages'
installed-version footer.

Each `scene-*.html` renders its own 1280×800 design inside a flex-centered viewport. A capture
tool that can't set the viewport to exactly 1280×800 (Chrome MCP's `resize_window` is ignored on
a maximized macOS window, for one) still gets a pixel-exact result if the canvas is pinned to the
viewport's top-left corner instead of being centered:

1. Serve `extension/` with any static file server — `python3 -m http.server 8802` run from the
   `extension/` directory — and open
   `http://localhost:8802/store-listing/screenshots/scene-job.html?theme=light` (also `?theme=dark`,
   and `scene-options.html?theme=light`).
2. Pin the canvas and drop its fit-to-viewport scale-down:

   ```js
   const st = document.createElement("style");
   st.textContent = `html,body{display:block!important;align-items:initial!important;
     justify-content:initial!important;overflow:hidden!important;}
     #canvas{position:absolute!important;top:0!important;left:0!important;transform:none!important;}`;
   document.head.appendChild(st);
   ```

   `#canvas.getBoundingClientRect()` should now read exactly `0,0,1280,800`.
3. Capture the region `0,0 → 1280,800`. Note that a capture tool's coordinate frame may be
   *smaller* than the CSS viewport (Chrome MCP reported 1528×784 for a 1728×887 viewport, a factor
   of 0.884), in which case the region has to be given in that frame — 1131×707 for this canvas.
   Chrome MCP's `zoom` action returns the region at roughly device-pixel resolution (1388×868
   here), which is larger than 1280×800 and therefore downscales cleanly.
4. Normalise: `sips -s format png -z 800 1280 raw.png --out popup-light.png`.
5. Shrink. A `sips` PNG of a 1280×800 UI screenshot lands around 350–450 KB; quantising to a
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

1. Copy `extension/` to a scratch directory, drop `store-listing/`, and add a `chrome-stub.js`
   that defines `chrome.runtime.getManifest`, `chrome.storage.local` (seeded with
   `afterapply_settings` holding a token, `afterapply_theme`, `afterapply_language: "tr"`,
   `afterapply_gmail_scan_enabled: true`), `chrome.tabs.query` returning a LinkedIn job URL, and
   `chrome.scripting.executeScript` returning a scraped-job object. Load it with a plain
   `<script src="chrome-stub.js">` placed **before** each page's `<script type="module">`, so the
   stub exists by the time `popup.js` runs `main()`.
2. Add a `frame.html` that centres an `<iframe>` of the page on a soft gradient inside a
   1280-wide, top-left-pinned canvas, sizes the iframe to `contentDocument`'s height once it has
   rendered, and applies a fixed `scale` (1.35 reads well for both pages).
3. Serve the scratch copy, open `frame.html?src=popup.html&w=360&scale=1.35` (and
   `?src=options.html&w=420&scale=1.35`), then capture and downscale as in step 3–4 above —
   these two are 1280 wide with a free height, since the help page renders them `w-full`.

The scratch copy exists so the stub never ships: `chrome-stub.js` must not end up in `extension/`.
