import { t } from "./i18n.js";

// The installed build number, rendered into the popup's and Settings' footers.
//
// Worth surfacing because a user is never obliged to update: a Chrome Web Store review can take
// days, and someone can sit on an old build indefinitely. So when the popup behaves differently
// from what a bug report describes, "which version is actually installed" is the first thing worth
// knowing — and reading it off chrome://extensions is a detour nobody should have to take.
//
// Read from the manifest rather than hardcoded, so it can never drift from what was shipped.
export function renderVersion(lang) {
  const el = document.getElementById("versionLabel");
  if (!el) {
    return;
  }

  el.textContent = t(lang, "versionLabel").replace("{version}", chrome.runtime.getManifest().version);
}
