import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { RELEASE_NOTES } from "../whats-new.js";

// The name and summary live in _locales since 0.9.2 and are copied by hand into the Web Store
// Dashboard from store-listing/LISTING.md. Nothing at runtime notices when the three drift apart,
// and the Store rejects a listing whose name does not match the package, so this pins them.
const read = (path) => readFileSync(join(import.meta.dirname, "..", path), "utf8");
const manifest = JSON.parse(read("manifest.json"));
const listing = read("store-listing/LISTING.md");
const LOCALES = ["en", "tr"];
const messages = Object.fromEntries(
  LOCALES.map((lang) => [lang, JSON.parse(read(`_locales/${lang}/messages.json`))]),
);

// The fenced block under "**EN**" / "**TR**" inside a "## <heading>" section of LISTING.md.
function listingBlock(heading, lang) {
  const section = listing.split(`## ${heading}`)[1].split("\n## ")[0];
  return section.split(`**${lang.toUpperCase()}**`)[1].split("```")[1].trim();
}

describe("the localized manifest", () => {
  it("takes its name and description from _locales, with a default locale that exists", () => {
    expect(manifest.name).toBe("__MSG_appName__");
    expect(manifest.description).toBe("__MSG_appDescription__");
    expect(LOCALES).toContain(manifest.default_locale);
  });

  it.each(LOCALES)("%s stays inside the Web Store limits and names no third-party product", (lang) => {
    const { appName, appDescription } = messages[lang];
    expect(appName.message.length).toBeLessThanOrEqual(75);
    expect(appDescription.message.length).toBeLessThanOrEqual(132);
    // 0.9.0 was rejected for keyword spam over a list of recruiting-system brands in the listing.
    expect(appName.message).not.toMatch(/linkedin|kariyer\.net|greenhouse|lever|workday/i);
    expect(appName.message.startsWith("e-kariyerim")).toBe(true);
  });

  it.each(LOCALES)("%s matches the store listing copy", (lang) => {
    expect(listingBlock("Item name", lang)).toBe(messages[lang].appName.message);
    expect(listingBlock("Summary (short description, max 132 characters)", lang)).toBe(
      messages[lang].appDescription.message,
    );
  });
});

// The popup's "what's new" banner reads its text from RELEASE_NOTES by the manifest's version. A
// version bump without notes would ship no banner at all, and nothing else would notice.
describe("release notes", () => {
  it("exist for the manifest's version, in both languages, item for item", () => {
    const notes = RELEASE_NOTES[manifest.version];

    expect(notes, `RELEASE_NOTES has no entry for ${manifest.version}`).toBeDefined();
    expect(notes.en.length).toBeGreaterThan(0);
    expect(notes.tr.length).toBe(notes.en.length);
    for (const item of [...notes.en, ...notes.tr]) {
      expect(item.trim()).not.toBe("");
    }
  });
});
