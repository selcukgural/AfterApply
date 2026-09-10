// Fetches/caches the local pre-filter scoring config gmail-scan.js runs against — plain script, no
// import/export: Chrome MV3 content scripts have no ES-module support (only background service
// workers and page scripts do), so this can't `import` storage.js. Shares this isolated-world
// global scope with gmail-scan.js, loaded right after it per manifest.json's content_scripts order.
//
// The fetch itself happens in background.js, not here: a content script's request carries the
// mail.google.com origin and is subject to that page's CORS, which the API doesn't allow (see
// background.js's own comment). This file only owns the cache and the fallback.

const AFTERAPPLY_FILTER_CONFIG_KEY = "afterapply_local_filter_config";

// Small, deliberately conservative fallback — used only before the first successful fetch (first
// run) or while fully offline. Real tuning lives server-side (appsettings.json's EmailIntelligence
// section) and reaches the extension via afterApplyGetLocalFilterConfig below, never via a Chrome
// Web Store release — this constant is expected to drift from the live config over time, which is
// an accepted, narrow-window risk (see the implementation plan's "explicitly out of scope").
const AFTERAPPLY_DEFAULT_LOCAL_FILTER_CONFIG = {
  threshold: 20,
  weights: {
    applicationPhrase: 20, interviewPhrase: 25, assessmentPhrase: 20, offerPhrase: 25,
    recruiterSignal: 10, knownJobBoardOrAts: 20, applicationLink: 10, calendarLink: 15,
    newsletter: -20, unsubscribe: -20, marketing: -20, jobAlert: -25, digest: -15,
    applicationCap: 30, interviewCap: 35, assessmentCap: 30, offerCap: 30, recruiterCap: 15,
    atsCap: 20, linksCap: 20, negativeCap: -30,
  },
  phrases: {
    application: ["application update", "application status", "your application", "applied for", "başvurunuz", "başvuru durumu"],
    interview: ["interview invitation", "interview scheduled", "technical interview", "mülakat", "görüşme"],
    assessment: ["assessment", "coding challenge", "değerlendirme testi"],
    offer: ["offer letter", "job offer", "iş teklifi", "teklif mektubu"],
    recruiter: ["recruiter", "talent acquisition", "hiring manager", "işe alım"],
    recruiterLocalPartPrefixes: ["recruiter", "recruitment", "talent", "careers", "jobs", "hiring", "hr-", "hr.", "hr_"],
    recruiterLocalPartExact: ["hr"],
    newsletter: ["newsletter", "bültenimiz"],
    unsubscribe: ["unsubscribe", "abonelikten çık"],
    marketing: ["promotional", "kampanya"],
    jobAlert: ["job alert", "recommended jobs", "önerilen ilanlar"],
    digest: ["weekly digest", "daily digest", "haftalık özet"],
    atsLinkDomains: ["greenhouse.io", "lever.co", "myworkdayjobs.com", "workday.com", "smartrecruiters.com", "ashbyhq.com"],
    calendarLinkDomains: ["calendly.com", "zoom.us", "teams.microsoft.com", "meet.google.com"],
  },
  jobBoardDomains: ["linkedin.com", "indeed.com", "kariyer.net", "greenhouse.io", "lever.co", "workday.com", "smartrecruiters.com"],
};

/** Asks background.js to make one allow-listed API call — see its AFTERAPPLY_ROUTES table. Shared
 * with gmail-scan.js via this file's isolated-world global scope, same as the config itself.
 * Resolves to the worker's plain result object, or a network-shaped one if the worker itself is
 * unreachable (mid-update, or torn down between the message and its reply). */
async function afterApplyCallApi(route, options = {}) {
  try {
    return await chrome.runtime.sendMessage({ type: "afterapply:api", route, ...options });
  } catch {
    return { ok: false, status: 0, reason: "worker-unreachable" };
  }
}

// Conditional-GET against the backend's ETag: a 304 keeps the cached config as-is (server-confirmed
// still fresh), a 200 replaces it. On any failure, falls back to the last-known-good cached config,
// then to the bundled default above — the local pre-filter must never be fully inert just because
// the network/backend is briefly unreachable.
async function afterApplyGetLocalFilterConfig() {
  const stored = await chrome.storage.local.get(AFTERAPPLY_FILTER_CONFIG_KEY);
  const cache = stored[AFTERAPPLY_FILTER_CONFIG_KEY] ?? null;

  const result = await afterApplyCallApi("local-filter-config", { ifNoneMatch: cache?.etag });

  if (result.status === 304 && cache) {
    await chrome.storage.local.set({ [AFTERAPPLY_FILTER_CONFIG_KEY]: { ...cache, fetchedAt: Date.now() } });
    return cache.config;
  }

  if (result.ok && result.body) {
    await chrome.storage.local.set({
      [AFTERAPPLY_FILTER_CONFIG_KEY]: { etag: result.etag, config: result.body, fetchedAt: Date.now() },
    });
    return result.body;
  }

  // Offline/unreachable/refused — fall back to cache, then to the bundled default. Not a correctness
  // boundary: the backend's own full-fidelity RecruitmentSignalAnalyzer + classifier still gate
  // everything that actually changes an application, this local scorer only decides what's worth
  // sending there.
  return cache?.config ?? AFTERAPPLY_DEFAULT_LOCAL_FILTER_CONFIG;
}
