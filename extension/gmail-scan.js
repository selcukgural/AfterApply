// Gmail content script — reads an opened thread's sender/subject/body directly from the DOM,
// scores it locally against local-filter-config.js's fetched config, and POSTs only the extracted
// signal for threads that pass the local threshold. Never reads/forwards the inbox list, never
// sends the raw email — see the implementation plan's "Context" for why (previous forward-all-mail
// design was rejected as invasive; this replaces it, scoped to threads the user actually opens).
//
// Plain script, not an ES module — content scripts can't `import` (see local-filter-config.js's own
// comment); shares this isolated-world global scope with it (loaded first, per manifest.json).

const AFTERAPPLY_GMAIL_SCAN_ENABLED_KEY = "afterapply_gmail_scan_enabled"; // must match storage.js
const AFTERAPPLY_SUBMITTED_IDS_KEY = "afterapply_gmail_submitted_ids";
const AFTERAPPLY_SUBMITTED_IDS_MAX = 500;
const AFTERAPPLY_CONFIG_REFRESH_MS = 10 * 60 * 1000; // 10 min — cheap conditional GET, see local-filter-config.js
const AFTERAPPLY_SCAN_DEBOUNCE_MS = 500;

let afterApplyConfig = null;
let afterApplyLastScannedThreadId = null;
let afterApplyScanTimer = null;

function afterApplyCountMatches(text, phrases) {
  return (phrases || []).filter((phrase) => text.includes(phrase.toLowerCase())).length;
}

function afterApplyCapped(hitCount, weightPerHit, cap) {
  if (hitCount === 0) return 0;
  const raw = hitCount * weightPerHit;
  return weightPerHit >= 0 ? Math.min(cap, raw) : Math.max(cap, raw);
}

function afterApplyMatchesAnyDomain(domain, knownDomains) {
  return (knownDomains || []).some((known) => domain === known || domain.endsWith(`.${known}`));
}

// JS mirror of RecruitmentSignalAnalyzer.Analyze's *shape* only (weighted phrase-hit categories,
// capped, positive minus negative, plus known-domain/link-domain bonuses) — deliberately simpler
// than the C# version: no MatchedApplication/CompanyNameInSubject cross-reference against the
// user's live application list (would need a broader API surface than "read this one thread").
// Only needs to be lenient enough to pass real signal through to the backend's full-fidelity
// analyzer, not equally precise — a false positive here costs one extra backend call.
function afterApplyScoreSignal(data, config) {
  const text = `${data.subject} ${data.snippet}`.toLowerCase();
  const w = config.weights;
  const p = config.phrases;

  let score = 0;
  score += afterApplyCapped(afterApplyCountMatches(text, p.application), w.applicationPhrase, w.applicationCap);
  score += afterApplyCapped(afterApplyCountMatches(text, p.interview), w.interviewPhrase, w.interviewCap);
  score += afterApplyCapped(afterApplyCountMatches(text, p.assessment), w.assessmentPhrase, w.assessmentCap);
  score += afterApplyCapped(afterApplyCountMatches(text, p.offer), w.offerPhrase, w.offerCap);

  const localPart = (data.senderEmail.split("@")[0] || "").toLowerCase();
  const hasRecruiterLocalPart =
    (p.recruiterLocalPartPrefixes || []).some((prefix) => localPart.startsWith(prefix)) ||
    (p.recruiterLocalPartExact || []).includes(localPart);
  const recruiterHits = afterApplyCountMatches(text, p.recruiter) + (hasRecruiterLocalPart ? 1 : 0);
  score += afterApplyCapped(recruiterHits, w.recruiterSignal, w.recruiterCap);

  score += afterApplyCapped(afterApplyCountMatches(text, p.newsletter), w.newsletter, w.negativeCap);
  score += afterApplyCapped(afterApplyCountMatches(text, p.unsubscribe), w.unsubscribe, w.negativeCap);
  score += afterApplyCapped(afterApplyCountMatches(text, p.marketing), w.marketing, w.negativeCap);
  score += afterApplyCapped(afterApplyCountMatches(text, p.jobAlert), w.jobAlert, w.negativeCap);
  score += afterApplyCapped(afterApplyCountMatches(text, p.digest), w.digest, w.negativeCap);

  const senderDomain = (data.senderEmail.split("@")[1] || "").toLowerCase();
  if (afterApplyMatchesAnyDomain(senderDomain, config.jobBoardDomains)) {
    score += Math.min(w.atsCap, w.knownJobBoardOrAts);
  }

  const linkDomains = data.linkDomains || [];
  const atsLinkHits = linkDomains.filter((domain) => afterApplyMatchesAnyDomain(domain, p.atsLinkDomains)).length;
  const calendarLinkHits = linkDomains.filter((domain) => afterApplyMatchesAnyDomain(domain, p.calendarLinkDomains)).length;
  const linksRaw = atsLinkHits * w.applicationLink + calendarLinkHits * w.calendarLink;
  if (linksRaw > 0) {
    score += Math.min(w.linksCap, linksRaw);
  }

  return Math.max(0, score);
}

// Reads the currently-open/expanded message only — verified live against real Gmail threads
// (span[email] for the real sender address, h2.hP for the subject, div.a3s for the body). Known
// simplification: takes the *first* matching element in the whole document rather than scoping to
// a specific message container, since Gmail auto-expands exactly one message (usually the latest)
// per open thread — in a multi-participant thread this could in rare cases pick up a CC'd
// participant's span before the actual sender's; worth hardening if that shows up in practice.
function afterApplyExtractOpenThread() {
  const senderSpan = document.querySelector("span[email]");
  const subjectEl = document.querySelector("h2.hP");
  const bodyEl = document.querySelector("div.a3s");

  if (!senderSpan || !subjectEl || !bodyEl) {
    return null;
  }

  const threadId = (location.hash || "").split("/").filter(Boolean).pop();
  if (!threadId) {
    return null;
  }

  const linkDomains = [...bodyEl.querySelectorAll("a[href]")]
    .map((a) => {
      try {
        return new URL(a.href).hostname.toLowerCase();
      } catch {
        return null;
      }
    })
    .filter((host, index, all) => host && all.indexOf(host) === index);

  return {
    threadId,
    senderEmail: senderSpan.getAttribute("email") || "",
    senderDisplayName: senderSpan.getAttribute("name") || "",
    subject: subjectEl.textContent || "",
    snippet: (bodyEl.textContent || "").slice(0, 2000), // capped client-side — this is the only body text ever read
    linkDomains,
  };
}

async function afterApplyWasAlreadySubmitted(threadId) {
  const stored = await chrome.storage.local.get(AFTERAPPLY_SUBMITTED_IDS_KEY);
  const ids = stored[AFTERAPPLY_SUBMITTED_IDS_KEY] ?? [];
  return ids.includes(threadId);
}

// Request-volume optimization only, not the correctness boundary — the backend's own
// ProviderMessageId idempotency check (a hash of this same thread id) is what actually prevents a
// duplicate suggestion if this dedup set is ever lost (extension reinstall, storage cleared, ...).
async function afterApplyMarkSubmitted(threadId) {
  const stored = await chrome.storage.local.get(AFTERAPPLY_SUBMITTED_IDS_KEY);
  const ids = stored[AFTERAPPLY_SUBMITTED_IDS_KEY] ?? [];
  ids.push(threadId);
  const trimmed = ids.length > AFTERAPPLY_SUBMITTED_IDS_MAX ? ids.slice(ids.length - AFTERAPPLY_SUBMITTED_IDS_MAX) : ids;
  await chrome.storage.local.set({ [AFTERAPPLY_SUBMITTED_IDS_KEY]: trimmed });
}

async function afterApplySubmitSignal(data, settings) {
  await fetch(`${settings.apiBaseUrl}/api/email-forwarding/extension-signal`, {
    method: "POST",
    headers: { Authorization: `Bearer ${settings.token}`, "Content-Type": "application/json" },
    body: JSON.stringify({
      senderEmail: data.senderEmail,
      senderDisplayName: data.senderDisplayName,
      subject: data.subject,
      snippet: data.snippet,
      receivedAt: new Date().toISOString(), // Gmail's own header timestamp isn't reliably DOM-exposed; approximate with scan time
      linkDomains: data.linkDomains,
      gmailMessageId: data.threadId,
    }),
  });
}

async function afterApplyScanCurrentThread() {
  try {
    const data = afterApplyExtractOpenThread();
    if (!data || data.threadId === afterApplyLastScannedThreadId) {
      return;
    }
    afterApplyLastScannedThreadId = data.threadId;

    if (!data.senderEmail || (await afterApplyWasAlreadySubmitted(data.threadId))) {
      return;
    }

    const score = afterApplyScoreSignal(data, afterApplyConfig);
    if (score < afterApplyConfig.threshold) {
      return;
    }

    const settings = await afterApplyGetSettings();
    if (!settings.token) {
      return; // scanning is inert without somewhere to send a signal
    }

    await afterApplySubmitSignal(data, settings);
    await afterApplyMarkSubmitted(data.threadId);
  } catch {
    // Best-effort — a transient DOM/storage/network hiccup here just means one thread's signal is
    // missed, not worth surfacing to Gmail's own console. Not a correctness boundary either way (see
    // afterApplyMarkSubmitted's comment on the backend idempotency check being the real one).
  }
}

function afterApplyScheduleScan() {
  clearTimeout(afterApplyScanTimer);
  afterApplyScanTimer = setTimeout(afterApplyScanCurrentThread, AFTERAPPLY_SCAN_DEBOUNCE_MS);
}

async function afterApplyInit() {
  const enabled = (await chrome.storage.local.get(AFTERAPPLY_GMAIL_SCAN_ENABLED_KEY))[AFTERAPPLY_GMAIL_SCAN_ENABLED_KEY];
  if (!enabled) {
    return; // non-negotiable: no DOM read, no fetch, nothing happens unless explicitly opted in
  }

  const settings = await afterApplyGetSettings();
  afterApplyConfig = await afterApplyGetLocalFilterConfig(settings.apiBaseUrl);
  setInterval(async () => {
    afterApplyConfig = await afterApplyGetLocalFilterConfig(settings.apiBaseUrl);
  }, AFTERAPPLY_CONFIG_REFRESH_MS);

  // Gmail is a single-page app — no full reload between the inbox and an opened thread, so a
  // MutationObserver on the main content area is the only way to notice "a thread just opened"
  // without the user re-clicking the toolbar icon every time.
  const observer = new MutationObserver(afterApplyScheduleScan);
  observer.observe(document.body, { childList: true, subtree: true });

  afterApplyScheduleScan();
}

afterApplyInit();
