# Screenshot sources

- `popup-light.png` / `popup-dark.png` — the popup auto-filling a LinkedIn job, light and dark.
- `options-light.png` — the Settings page.

All three are 1280×800 PNGs (the Chrome Web Store's required screenshot size) generated from the
`scene-*.html` files in this folder, which are marketing compositions, not the shipped extension
pages — they reuse `../../popup.css`'s real classes directly (`.app-header`, `.site-badge`,
labels/inputs/buttons, `.theme-toggle`) with static sample data, so they can't visually drift from
the real pages, but they render outside a real browser-extension context. Neither this folder nor
`store-listing/` is part of the packaged extension (see `PUBLISHING_CHECKLIST.md`'s zip command).

## Regenerating them

Each `scene-*.html` renders its own 1280×800 design at a fixed 0.6 CSS `transform: scale()` inside
a flex-centered, `overflow: hidden` viewport — this makes the capture resilient to the actual
browser window/viewport size not matching 1280×800 exactly (screenshot tooling here doesn't
reliably honor a requested window size). The capture therefore comes back smaller than 1280×800
and off-scale; recover the exact size with a centered crop + upscale, which works because the
design is flex-centered so the canvas's center coincides with the screenshot's center regardless
of capture resolution:

```bash
sips -c 480 768 raw-capture.jpg --out cropped.png   # centered crop, 1280:800 aspect ratio
sips -z 800 1280 cropped.png --out final.png         # scale back up to exact 1280x800
```

To change the sample content (job title/company, headline copy, theme), edit the `scene-*.html`
file directly, serve `extension/` with any static file server (e.g. `python3 -m http.server` run
from the `extension/` directory), open `store-listing/screenshots/scene-job.html?theme=light` (or
`?theme=dark`) / `scene-options.html?theme=light`, screenshot it, and run it through the crop
commands above.
