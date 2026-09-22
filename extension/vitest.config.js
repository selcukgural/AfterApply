import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    // Only the DOM-reading half needs a document; the URL/id half is pure. happy-dom is far
    // lighter than jsdom and enough for querySelector plus innerHTML fixtures.
    environment: "happy-dom",
    environmentOptions: {
      // The sanitizer fixtures deliberately contain a <script> tag — that is the point of the
      // test. happy-dom would otherwise *run* it while parsing, so the suite would fail on the
      // very input it exists to prove is stripped. A real browser page is not the environment
      // here: the injected scraper only ever reads a document the browser already parsed.
      happyDOM: { settings: { disableJavaScriptEvaluation: true } },
    },
    include: ["tests/**/*.test.js"],
  },
});
