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

Each `scene-*.html` renders its own 1280×800 design inside a flex-centered, `overflow: hidden`
viewport, scaled down by a fixed `transform: scale(0.6)` so the whole canvas is visible even in a
viewport smaller than 1280×800. If the capture tool can size the viewport, capture at 1:1 instead
— it is sharper than any crop-and-upscale:

1. Serve `extension/` with any static file server (e.g. `python3 -m http.server` run from the
   `extension/` directory).
2. Open `store-listing/screenshots/scene-job.html?theme=light` (or `?theme=dark`) /
   `scene-options.html?theme=light`.
3. Size the *viewport* (not the window) to exactly 1280×800 and drop the scale-down:
   `document.getElementById('canvas').style.transform = 'scale(1)'`.
4. Screenshot, then normalise whatever the tool hands back to the exact required size:
   `sips -z 800 1280 raw-capture.png --out final.png`.

If the viewport can't be sized, leave the 0.6 scale in place and recover 1280×800 with a centered
crop + upscale instead — the design is flex-centered, so the canvas's center is the screenshot's
center at any capture resolution:

```bash
sips -c 480 768 raw-capture.jpg --out cropped.png   # centered crop, 1280:800 aspect ratio
sips -z 800 1280 cropped.png --out final.png        # scale back up to exact 1280x800
```

To change the sample content (job title/company, headline copy, theme), edit the `scene-*.html`
file directly and reshoot.
