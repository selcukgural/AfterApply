// "What's new" after an update, and "an update is downloaded" before one is applied (0.9.2).
//
// Chrome updates the extension on its own, so nobody needs to be told to update — but nobody
// notices that anything changed either. background.js records an update the moment it lands
// (chrome.runtime.onInstalled) and puts a dot on the toolbar icon; the popup clears the dot when
// it opens and shows these notes above the form until they are closed. Everything stays in this
// browser: no request is made and nothing is reported back.
//
// background.js cannot import this file (it is a plain script, see its header), so the two storage
// keys below are duplicated there and must stay in step.

export const WHATS_NEW_KEY = "afterapply_whats_new";
export const PENDING_UPDATE_KEY = "afterapply_pending_update";

// One entry per released version, in both languages; tests/manifest.test.js fails when the
// manifest's version has none, so a release cannot ship an empty banner or no banner by accident.
// Plain text only — rendered with textContent.
export const RELEASE_NOTES = {
  "0.9.2": {
    en: [
      "Save a posting you have not applied to yet with \"Apply later\". When you apply, click \"I Applied\" on the same page and it moves to your applications.",
      "The LinkedIn hiring-team contact fills the HR fields again.",
      "The extension now has a Turkish name: Başvurunu Kaydet.",
    ],
    tr: [
      "Henüz başvurmadığınız ilanı \"Sonra başvur\" ile kaydedin. Başvurduğunuzda aynı sayfada \"Başvurdum\"a basın; kayıt başvurularınıza taşınır.",
      "LinkedIn'deki işe alım ekibi kişisi İK alanlarına yine doluyor.",
      "Eklentinin artık Türkçe bir adı var: Başvurunu Kaydet.",
    ],
  },
};

/** Numeric, dot-separated comparison ("0.10.0" is newer than "0.9.2"). Negative, zero or positive. */
export function compareVersions(a, b) {
  const left = String(a).split(".").map(Number);
  const right = String(b).split(".").map(Number);
  for (let i = 0; i < Math.max(left.length, right.length); i++) {
    const diff = (left[i] || 0) - (right[i] || 0);
    if (diff !== 0) {
      return diff;
    }
  }
  return 0;
}

/**
 * What the popup shows, from what storage holds and the installed version.
 *
 * - notes: this version's notes in the chosen language, or null when it has none.
 * - bannerOpen: an update to this version was recorded and its notes have not been closed yet. A
 *   fresh install records nothing, so a new user is not greeted with a changelog.
 * - clearBadge: the dot is still on the icon; opening the popup is the moment to take it off.
 * - pendingVersion: a newer build is downloaded and waiting for a restart. A recorded version that
 *   is not newer than the running one is stale (the update already applied) and ignored.
 */
export function noticeState({ whatsNew, pendingUpdate }, currentVersion, lang) {
  const entry = RELEASE_NOTES[currentVersion];
  const notes = entry ? entry[lang] ?? entry.en : null;
  const recorded = whatsNew?.version === currentVersion;

  return {
    notes,
    bannerOpen: Boolean(notes && recorded && !whatsNew.dismissed),
    clearBadge: Boolean(recorded && whatsNew.badge),
    pendingVersion: pendingUpdate && compareVersions(pendingUpdate, currentVersion) > 0 ? pendingUpdate : null,
  };
}
