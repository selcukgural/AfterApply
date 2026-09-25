import { getSettings, daysUntilExpiry, EXPIRY_WARNING_DAYS } from "./storage.js";
import { setUpThemeToggle } from "./theme.js";
import { t, setUpLanguageToggle } from "./i18n.js";
import { renderVersion } from "./version.js";
import { detectJob, scrapeConfigFor } from "./adapters.js";
import { scrapeJobPosting } from "./scraper.js";
import { WHATS_NEW_KEY, PENDING_UPDATE_KEY, noticeState } from "./whats-new.js";

const content = document.getElementById("content");

// screen: "noJob" | "noToken" | "tokenExpired" | "permission" | "form" (unset while the initial async
// detection/scrape is still in flight — the header's skeleton placeholder stays visible until one
// of these lands). "tokenExpired" is told apart from "noToken" on purpose: until this version the
// connection simply stopped working after ninety days and said "could not reach e-kariyerim",
// which is what a lost network connection says too.
// statusKey/statusType track the form's status paragraph so a language toggle mid-flow re-renders
// it in the new language too, without touching anything else (see render() below).
const state = {
  lang: "en",
  screen: null,
  job: null,
  jobUrl: null,
  settings: null,
  scraped: null,
  scrapeError: null,
  statusKey: null,
  statusType: null,
  // Set when the tab's origin is not in host_permissions and the user has not granted it at
  // runtime yet: the popup then offers the grant instead of reading the page. LinkedIn and
  // kariyer.net are still in the manifest, so neither ever reaches this state (0.9.0).
  permissionNeeded: false,
  // The tab the popup was opened over; kept because the scrape can now happen a step later, after
  // a permission grant, when chrome.tabs.query would be a second round trip for the same answer.
  tabId: null,
  // Days left on the connection when it is close to lapsing, null otherwise. See main().
  expiryWarningDays: null,
  // What whats-new.js reads: the recorded update and a downloaded-but-waiting version.
  notices: { whatsNew: null, pendingUpdate: null },
};

// LinkedIn's company anchor href carries the canonical /company/<slug>/ path but is sometimes
// suffixed with tracking query params/a fragment depending on which page layout rendered it —
// stripped here so the URL we store (and CompanyEnrichmentService later re-fetches server-side)
// is stable and matches what a plain HTTP GET of the company page actually resolves.
function canonicalizeLinkedInCompanyUrl(href) {
  if (!href) {
    return null;
  }
  let parsed;
  try {
    parsed = new URL(href);
  } catch {
    return null;
  }
  const match = parsed.pathname.match(/^\/company\/([^/]+)/);
  return match ? `https://www.linkedin.com/company/${match[1]}/` : null;
}

// kariyer.net's company anchor on a job page points at /firma-profil/<slug>-<id> — the site's own
// counterpart to LinkedIn's /company/<slug>/, and (unlike LinkedIn) the only page that publishes
// the company's own website. Same canonicalization reasoning as the LinkedIn one above: strip
// query params/fragment so what we store matches what a plain server-side GET later resolves.
function canonicalizeKariyerNetCompanyUrl(href) {
  if (!href) {
    return null;
  }
  let parsed;
  try {
    parsed = new URL(href);
  } catch {
    return null;
  }
  const match = parsed.pathname.match(/^\/firma-profil\/([^/]+)/);
  return match ? `https://www.kariyer.net/firma-profil/${match[1]}` : null;
}

// LinkedIn's hiring-team card links the poster's profile at /in/<slug>. Canonicalized the same way
// as the company URL: the stored value is validated server-side against an https://linkedin.com
// /in/... allow-list, and the slug stays percent-encoded because LinkedIn's own slugs contain
// non-ASCII characters (e.g. /in/çiğdem-çağ-kara-b8194077).
function canonicalizeLinkedInProfileUrl(href) {
  if (!href) {
    return null;
  }
  let parsed;
  try {
    parsed = new URL(href);
  } catch {
    return null;
  }
  const match = parsed.pathname.match(/^\/in\/([^/]+)/);
  return match ? `https://www.linkedin.com/in/${match[1]}/` : null;
}

// Mailboxes nobody reads — mirrors HrEmailCandidate.AutomatedMarkers on the backend, which is the
// real gate; this copy only avoids showing the user a prefilled address that the server would
// refuse to treat as a contact anyway.
function looksAutomated(email) {
  const localPart = email.split("@")[0].replace(/[.\-_]/g, "");
  return ["noreply", "donotreply", "nepasrepondre", "mailerdaemon", "postmaster", "bounce", "autoreply", "automated", "notification"]
    .some((marker) => localPart.includes(marker));
}

// An address spelled out in the posting body. Measured 2026-09-06: essentially never present (0 of
// 35 kariyer.net postings, and LinkedIn's guest pages showed none either) — both sites want the
// application to go through their own funnel. Kept because the occasional employer does write
// "send your CV to ...", and it costs nothing when there is nothing to find. Deliberately requires
// exactly one distinct candidate: a posting quoting several addresses gives us no way to tell
// which one is the recruiter, and guessing would put a stranger's address on the record.
function extractContactEmail(text) {
  if (!text) {
    return null;
  }
  const matches = text.match(/[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}/g) || [];
  const candidates = [...new Set(matches.map((m) => m.toLowerCase()))].filter((m) => !looksAutomated(m));
  return candidates.length === 1 ? candidates[0] : null;
}

function setContent(html) {
  content.innerHTML = html;
}

/**
 * Hands the pairing over to the options page, which then starts it on its own (?pair=1). It cannot
 * run here: a popup is destroyed the moment focus leaves it, and the very next step — opening the
 * confirmation page in a tab — does exactly that, so a handshake started in the popup would die
 * before anyone could confirm it.
 */
function openPairing() {
  chrome.tabs.create({ url: chrome.runtime.getURL("options.html?pair=1") });
}

function renderMessage(text, linkText, onLinkClick) {
  setContent(`<p class="muted">${text}</p>`);
  if (linkText) {
    const button = document.createElement("button");
    button.className = "secondary";
    button.textContent = linkText;
    button.addEventListener("click", onLinkClick);
    content.appendChild(button);
  }
}

// Minimal click-to-select typeahead against /api/companies/search — deliberately no keyboard
// navigation (not a "smart" system by design, see DECISIONS.md), just enough to steer users
// toward an existing Company instead of typing a near-duplicate variant.
function setUpCompanyAutocomplete(settings) {
  const input = document.getElementById("companyName");
  const list = document.getElementById("companySuggestions");
  let debounceHandle = null;
  let requestId = 0;

  function hideSuggestions() {
    list.hidden = true;
    list.innerHTML = "";
  }

  async function search(query) {
    const currentRequestId = ++requestId;
    let results = [];
    try {
      const response = await fetch(`${settings.apiBaseUrl}/api/companies/search?q=${encodeURIComponent(query)}`, {
        headers: { Authorization: `Bearer ${settings.token}` },
      });
      if (response.ok) {
        results = await response.json();
      }
    } catch {
      // Autocomplete is a convenience — a network hiccup here just means no suggestions,
      // never blocks typing/submitting the form by hand.
      results = [];
    }

    if (currentRequestId !== requestId) {
      return; // A newer keystroke already superseded this request.
    }

    if (results.length === 0) {
      hideSuggestions();
      return;
    }

    list.innerHTML = results
      .map((company) => `<li><button type="button" data-name="${escapeHtml(company.name)}">${escapeHtml(company.name)}</button></li>`)
      .join("");
    list.hidden = false;
  }

  input.addEventListener("input", () => {
    if (debounceHandle) {
      clearTimeout(debounceHandle);
    }
    const query = input.value.trim();
    if (query.length < 2) {
      hideSuggestions();
      return;
    }
    debounceHandle = setTimeout(() => search(query), 250);
  });

  list.addEventListener("click", (event) => {
    const button = event.target.closest("button[data-name]");
    if (!button) {
      return;
    }
    input.value = button.dataset.name;
    hideSuggestions();
  });

  document.addEventListener("click", (event) => {
    if (event.target !== input && !list.contains(event.target)) {
      hideSuggestions();
    }
  });
}

function renderStatus() {
  const statusEl = document.getElementById("status");
  if (!statusEl || !state.statusKey) {
    return;
  }
  statusEl.textContent = t(state.lang, state.statusKey);
  statusEl.className = `status ${state.statusType}`;
  statusEl.hidden = false;
}

function setStatus(key, type) {
  state.statusKey = key;
  state.statusType = type;
  renderStatus();
}

// Re-labels the already-built form in place — never touches input values or rebuilds the DOM, so
// a language toggle mid-edit can't wipe what the user has typed (the scrape is often imperfect;
// editing is expected). Only buildForm() (called once, right after a successful scrape) creates
// the form markup itself.
function relabelForm() {
  const lang = state.lang;
  const setText = (id, key) => {
    const el = document.getElementById(id);
    if (el) {
      el.textContent = t(lang, key);
    }
  };

  setText("companyLabelEl", "popup.companyLabel");
  setText("jobTitleLabelEl", "popup.jobTitleLabel");
  setText("locationLabelEl", "popup.locationLabel");
  setText("hrNameLabelEl", "popup.hrNameLabel");
  setText("hrEmailLabelEl", "popup.hrEmailLabel");
  setText("hrLinkedInLabelEl", "popup.hrLinkedInLabel");
  setText("submit", "popup.applyButton");
  setText("laterLabelEl", "popup.laterButton");

  const expiryEl = document.getElementById("expiryWarning");
  if (expiryEl) {
    expiryEl.textContent = t(lang, "popup.expiryWarning").replace("{days}", state.expiryWarningDays);
  }

  const provenanceEl = document.getElementById("provenance");
  if (provenanceEl && state.scraped) {
    provenanceEl.textContent = t(lang, state.scraped.foundBy === "meta" ? "popup.guessed" : "popup.autoDetected");
  }

  const errorEl = document.getElementById("scrapeError");
  if (errorEl) {
    errorEl.textContent = `${t(lang, "popup.autoFillFailed")}${state.scrapeError}`;
  }

  renderStatus();
}

// ---- Update notices (0.9.2) ----------------------------------------------------------------------
// Built with DOM APIs, never innerHTML: the text is ours today, but this is the one part of the
// popup that shows a version string read back out of storage.

const CURRENT_VERSION = chrome.runtime.getManifest().version;

async function loadNotices() {
  try {
    const stored = await chrome.storage.local.get([WHATS_NEW_KEY, PENDING_UPDATE_KEY]);
    state.notices = { whatsNew: stored[WHATS_NEW_KEY] ?? null, pendingUpdate: stored[PENDING_UPDATE_KEY] ?? null };

    // Opening the popup is what the dot asked for: take it off, keep the banner until it is closed.
    if (noticeState(state.notices, CURRENT_VERSION, state.lang).clearBadge) {
      await saveWhatsNew({ ...state.notices.whatsNew, badge: false });
      await chrome.action.setBadgeText({ text: "" });
    }
  } catch (error) {
    // A nicety: a storage hiccup here must never stand between the user and the form.
    console.warn("[e-kariyerim] could not read the update notices", error);
  }
}

async function saveWhatsNew(whatsNew) {
  state.notices = { ...state.notices, whatsNew };
  await chrome.storage.local.set({ [WHATS_NEW_KEY]: whatsNew });
}

async function setBannerOpen(open) {
  try {
    await saveWhatsNew({ version: CURRENT_VERSION, dismissed: !open, badge: false });
  } catch (error) {
    console.warn("[e-kariyerim] could not save the notice state", error);
  }
  renderNotices();
}

function closeIcon() {
  const svgNs = "http://www.w3.org/2000/svg";
  const svg = document.createElementNS(svgNs, "svg");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("fill", "none");
  svg.setAttribute("stroke", "currentColor");
  svg.setAttribute("stroke-width", "2");
  svg.setAttribute("stroke-linecap", "round");
  svg.setAttribute("aria-hidden", "true");
  const path = document.createElementNS(svgNs, "path");
  path.setAttribute("d", "M6 6l12 12M18 6L6 18");
  svg.appendChild(path);
  return svg;
}

function buildWhatsNew(notes) {
  const lang = state.lang;
  const section = document.createElement("section");
  section.className = "notice";
  section.setAttribute("aria-label", t(lang, "popup.whatsNewLink"));

  const head = document.createElement("div");
  head.className = "notice-head";
  const title = document.createElement("p");
  title.className = "notice-title";
  title.textContent = t(lang, "popup.whatsNewTitle").replace("{version}", CURRENT_VERSION);
  const close = document.createElement("button");
  close.type = "button";
  close.className = "notice-close";
  close.setAttribute("aria-label", t(lang, "popup.close"));
  close.appendChild(closeIcon());
  close.addEventListener("click", () => setBannerOpen(false));
  head.append(title, close);

  const list = document.createElement("ul");
  for (const note of notes) {
    const item = document.createElement("li");
    item.textContent = note;
    list.appendChild(item);
  }

  section.append(head, list);
  return section;
}

function buildUpdateRow(version) {
  const lang = state.lang;
  const row = document.createElement("section");
  row.className = "update-row";
  row.setAttribute("aria-label", t(lang, "popup.updateReady").replace("{version}", version));

  const text = document.createElement("div");
  text.className = "update-row-text";
  const title = document.createElement("strong");
  title.textContent = t(lang, "popup.updateReady").replace("{version}", version);
  const hint = document.createElement("span");
  hint.textContent = t(lang, "popup.updateReadyHint");
  text.append(title, hint);

  const restart = document.createElement("button");
  restart.type = "button";
  restart.textContent = t(lang, "popup.updateRestart");
  // Restarts into the downloaded build; the popup closes with it.
  restart.addEventListener("click", () => chrome.runtime.reload());

  row.append(text, restart);
  return row;
}

function renderNotices() {
  const container = document.getElementById("notices");
  const link = document.getElementById("whatsNewLink");
  if (!container || !link) {
    return;
  }

  const notices = noticeState(state.notices, CURRENT_VERSION, state.lang);
  container.replaceChildren();
  if (notices.pendingVersion) {
    container.appendChild(buildUpdateRow(notices.pendingVersion));
  }
  if (notices.bannerOpen) {
    container.appendChild(buildWhatsNew(notices.notes));
  }
  container.hidden = container.childElementCount === 0;

  link.textContent = t(state.lang, "popup.whatsNewLink");
  link.hidden = !notices.notes || notices.bannerOpen;
}

function render() {
  const lang = state.lang;
  document.title = t(lang, "popup.pageTitle");
  // Outside the screen branches below: the footer is there on every screen, including "no job
  // here" and "no token yet", which are exactly the moments someone checks their version.
  renderVersion(lang);
  // Same: an update is worth mentioning whichever screen the popup opened on.
  renderNotices();

  if (state.screen === "form") {
    relabelForm();
  } else if (state.screen === "permission") {
    renderPermissionRequest();
  } else if (state.screen === "noJob") {
    // The manual path is what keeps an unreadable page from being a dead end: the URL is known,
    // so two typed fields still produce a tracked application.
    renderMessage(t(lang, "popup.noJob"), t(lang, "popup.addManually"), startManualEntry);
  } else if (state.screen === "noToken") {
    renderMessage(t(lang, "popup.noToken"), t(lang, "popup.connect"), openPairing);
  } else if (state.screen === "tokenExpired") {
    renderMessage(t(lang, "popup.tokenExpired"), t(lang, "popup.connect"), openPairing);
  }
}

/**
 * The runtime permission ask. Chrome only grants a host permission from a user gesture, so this
 * has to be a button in the popup — and it is the honest place for it anyway: the extension is
 * asking to read the page in front of the user, at the moment they asked it to.
 *
 * The site is named either by its adapter ("Greenhouse posting detected") or by its bare hostname,
 * which is the difference between "we know this board" and "this is a page you are adding".
 */
function renderPermissionRequest() {
  const lang = state.lang;
  const job = state.job;
  const known = job.site !== "generic";
  const message = t(lang, known ? "popup.permissionKnownSite" : "popup.permissionUnknownSite").replace("{site}", job.label);

  renderMessage(message, t(lang, "popup.permissionGrant"), async () => {
    let granted = false;
    try {
      granted = await chrome.permissions.request({ origins: [`${job.origin}/*`] });
    } catch {
      // Chrome refuses the request outright when the popup lost its user gesture (the window was
      // clicked away and back). Treated as "not granted": the manual path is still there.
      granted = false;
    }

    if (!granted) {
      renderMessage(t(state.lang, "popup.permissionDenied"), t(state.lang, "popup.addManually"), startManualEntry);
      return;
    }

    await scrapeAndShowForm();
  });
}

/** The blank form for a page nothing could be read from. Everything the backend requires is typed
 * by the user; the URL still comes from the tab, so the application points at the real posting. */
function startManualEntry() {
  state.scraped = emptyScrape();
  state.scrapeError = null;
  state.screen = "form";
  buildForm();
}

function emptyScrape() {
  return {
    title: "", company: "", location: "", description: null, descriptionHtml: null,
    publishedAt: null, companyLinkedInUrl: null, companyKariyerNetUrl: null,
    hrName: null, hrEmail: null, hrLinkedInUrl: null, foundBy: null,
  };
}

function buildForm() {
  const lang = state.lang;
  const { job, scraped, settings } = state;

  setContent(`
    <span class="site-badge">${escapeHtml(job.label)}</span>
    ${scraped.foundBy === "jsonld" ? `<span id="provenance" class="site-badge muted-badge">${escapeHtml(t(lang, "popup.autoDetected"))}</span>` : ""}
    ${scraped.foundBy === "meta" ? `<span id="provenance" class="site-badge muted-badge">${escapeHtml(t(lang, "popup.guessed"))}</span>` : ""}
    ${state.expiryWarningDays === null ? "" : `<p id="expiryWarning" class="status warning">${escapeHtml(t(lang, "popup.expiryWarning").replace("{days}", state.expiryWarningDays))}</p>`}
    ${state.scrapeError ? `<p id="scrapeError" class="status error">${escapeHtml(t(lang, "popup.autoFillFailed"))}${escapeHtml(state.scrapeError)}</p>` : ""}
    <label id="companyLabelEl" for="companyName">${escapeHtml(t(lang, "popup.companyLabel"))}</label>
    <div class="combobox">
      <input id="companyName" type="text" autocomplete="off" value="${escapeHtml(scraped.company)}" />
      <ul id="companySuggestions" class="suggestions" hidden></ul>
    </div>

    <label id="jobTitleLabelEl" for="jobTitle">${escapeHtml(t(lang, "popup.jobTitleLabel"))}</label>
    <input id="jobTitle" type="text" value="${escapeHtml(scraped.title)}" />

    <label id="locationLabelEl" for="location">${escapeHtml(t(lang, "popup.locationLabel"))}</label>
    <input id="location" type="text" value="${escapeHtml(scraped.location)}" />

    <label id="hrNameLabelEl" for="hrName">${escapeHtml(t(lang, "popup.hrNameLabel"))}</label>
    <input id="hrName" type="text" value="${escapeHtml(scraped.hrName ?? "")}" />

    <label id="hrEmailLabelEl" for="hrEmail">${escapeHtml(t(lang, "popup.hrEmailLabel"))}</label>
    <input id="hrEmail" type="email" value="${escapeHtml(scraped.hrEmail ?? "")}" />

    <label id="hrLinkedInLabelEl" for="hrLinkedInUrl">${escapeHtml(t(lang, "popup.hrLinkedInLabel"))}</label>
    <input id="hrLinkedInUrl" type="text" value="${escapeHtml(scraped.hrLinkedInUrl ?? "")}" />

    <div id="actionRow" class="action-row"></div>
    <p id="status" class="status" hidden></p>
  `);

  buildActionRow();

  setUpCompanyAutocomplete(settings);
}

function bookmarkIcon() {
  const svgNs = "http://www.w3.org/2000/svg";
  const svg = document.createElementNS(svgNs, "svg");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("fill", "none");
  svg.setAttribute("stroke", "currentColor");
  svg.setAttribute("stroke-width", "2");
  svg.setAttribute("stroke-linecap", "round");
  svg.setAttribute("stroke-linejoin", "round");
  svg.setAttribute("aria-hidden", "true");
  const path = document.createElementNS(svgNs, "path");
  path.setAttribute("d", "M6 3h12v18l-6-4-6 4z");
  svg.appendChild(path);
  return svg;
}

/** "Apply later" and "I Applied", side by side (0.9.2, canvas variant B). Built with DOM APIs. */
function buildActionRow() {
  const row = document.getElementById("actionRow");

  const later = document.createElement("button");
  later.id = "saveLater";
  later.type = "button";
  later.className = "outline";
  const laterLabel = document.createElement("span");
  laterLabel.id = "laterLabelEl";
  laterLabel.textContent = t(state.lang, "popup.laterButton");
  later.append(bookmarkIcon(), laterLabel);

  const apply = document.createElement("button");
  apply.id = "submit";
  apply.type = "button";
  apply.textContent = t(state.lang, "popup.applyButton");

  row.append(later, apply);

  later.addEventListener("click", () => submitCapture("later"));
  apply.addEventListener("click", () => submitCapture("apply"));
}

// What each "Apply later" outcome says. Anything unrecognised reads as saved: the request
// succeeded, and a newer backend adding an outcome must not turn into an error here.
const LATER_STATUS = {
  Saved: "popup.savedForLater",
  AlreadySaved: "popup.alreadySaved",
  AlreadyApplied: "popup.alreadyApplied",
};

/**
 * Sends the form. Both buttons send the same capture; only the route and the reading of the answer
 * differ. "later" saves it as a TrackedJob; "apply" logs the application, which on the server also
 * turns a posting saved earlier with "later" into that application.
 */
async function submitCapture(kind) {
  const { scraped, settings } = state;
  const laterButton = document.getElementById("saveLater");
  const applyButton = document.getElementById("submit");
  const setBusy = (busy) => {
    laterButton.disabled = busy;
    applyButton.disabled = busy;
  };

  setBusy(true);
  state.statusKey = null;
  document.getElementById("status").hidden = true;

  const companyName = document.getElementById("companyName").value.trim();
  const jobTitle = document.getElementById("jobTitle").value.trim();
  const location = document.getElementById("location").value.trim();
  const hrName = document.getElementById("hrName").value.trim();
  const hrEmail = document.getElementById("hrEmail").value.trim();
  const hrLinkedInUrl = document.getElementById("hrLinkedInUrl").value.trim();

  if (!companyName || !jobTitle) {
    setStatus("popup.requiredFields", "error");
    setBusy(false);
    return;
  }

  const path = kind === "later" ? "/api/tracked-jobs/from-extension" : "/api/applications/from-extension";

  try {
    const response = await fetch(`${settings.apiBaseUrl}${path}`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${settings.token}`,
      },
      body: JSON.stringify({
        companyName,
        jobTitle,
        jobUrl: state.jobUrl,
        location: location || null,
        description: scraped.description,
        descriptionHtml: scraped.descriptionHtml,
        publishedAt: scraped.publishedAt,
        companyLinkedInUrl: scraped.companyLinkedInUrl,
        companyKariyerNetUrl: scraped.companyKariyerNetUrl,
        // Derived from the posting URL rather than scraped (adapters.js), and pinned server-side
        // to the ATS allow-list because the enrichment job fetches it.
        companyAtsUrl: state.job.companyAtsUrl ?? null,
        hrName: hrName || null,
        hrEmail: hrEmail || null,
        hrLinkedInUrl: hrLinkedInUrl || null,
      }),
    });

    if (response.status === 401) {
      // Not a network problem and not a bad payload: the token is gone, revoked or past its
      // ninety days. Says so, instead of blaming the connection.
      setStatus("popup.unauthorized", "error");
      setBusy(false);
      return;
    }

    if (!response.ok) {
      throw new Error(`Request failed (${response.status})`);
    }

    const result = await response.json();

    if (kind === "later") {
      setStatus(LATER_STATUS[result.outcome] ?? LATER_STATUS.Saved, "success");
      // "I Applied" stays available — saving now and applying a minute later is the point —
      // unless the posting is already an application and there is nothing left to press.
      laterButton.disabled = true;
      applyButton.disabled = result.outcome === "AlreadyApplied";
      return;
    }

    let key = "popup.added";
    if (result.wasDuplicate) {
      key = "popup.alreadyTracked";
    } else if (result.fromTrackedJob) {
      key = "popup.movedFromSaved";
    }
    setStatus(key, "success");
  } catch {
    setStatus("popup.networkError", "error");
    setBusy(false);
  }
}

async function main() {
  setUpThemeToggle("themeToggle");
  document.getElementById("openOptionsBtn")?.addEventListener("click", () => chrome.runtime.openOptionsPage());
  document.getElementById("whatsNewLink")?.addEventListener("click", () => setBannerOpen(true));
  // Before the first render, so the banner is there from the first frame rather than popping in.
  await loadNotices();
  await setUpLanguageToggle("langToggle", (lang) => {
    state.lang = lang;
    render();
  });

  state.settings = await getSettings();

  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  // Null only for a page the popup can never read — a chrome:// page, the Web Store, a plain
  // http:// site. Every https page gets a descriptor, because since 0.9.0 the popup can offer to
  // read any of them (adapters.js).
  const job = tab?.url ? detectJob(tab.url) : null;
  if (!job) {
    state.screen = "noJob";
    render();
    return;
  }
  state.job = job;
  state.jobUrl = job.jobUrl;
  state.tabId = tab.id;

  if (!state.settings.token) {
    state.screen = "noToken";
    render();
    return;
  }

  const daysLeft = daysUntilExpiry(state.settings.tokenExpiresAt);
  if (daysLeft !== null && daysLeft < 0) {
    state.screen = "tokenExpired";
    render();
    return;
  }
  // Shown above the form rather than instead of it: the connection still works today, and
  // interrupting a job someone is in the middle of saving would be the wrong trade.
  state.expiryWarningDays = daysLeft !== null && daysLeft <= EXPIRY_WARNING_DAYS ? daysLeft : null;

  // LinkedIn, kariyer.net and the Gmail origin are in manifest host_permissions, so this is true
  // for them without anyone ever being asked — the 0.8.0 flow is unchanged. Anything else needs
  // the runtime grant, which is the whole reason the extension can now capture from a site that
  // was never in its manifest.
  const hasPermission = await chrome.permissions.contains({ origins: [`${job.origin}/*`] }).catch(() => false);
  if (!hasPermission) {
    state.permissionNeeded = true;
    state.screen = "permission";
    render();
    return;
  }

  await scrapeAndShowForm();
}

/** Reads the page and shows the form. Shared by the first run and by the run that follows a
 * granted permission, so both paths end in exactly the same place. */
async function scrapeAndShowForm() {
  const job = state.job;
  let scraped;
  let scrapeError = null;
  try {
    const [{ result }] = await chrome.scripting.executeScript({
      target: { tabId: state.tabId },
      func: scrapeJobPosting,
      args: [scrapeConfigFor(job)],
    });
    scraped = result ?? emptyScrape();
  } catch (error) {
    scraped = emptyScrape();
    // Surfaced inline (not just console.error) so a manual tester doesn't need DevTools open to
    // see why fields came back empty — a silently-swallowed error makes a real scrape failure look
    // identical to "nothing found".
    scrapeError = error?.message || String(error);
  }

  // The injected scraper returns raw hrefs — tracking-param stripping happens here, back in the
  // extension's own scope, because it cannot reach these helpers.
  scraped.companyLinkedInUrl = canonicalizeLinkedInCompanyUrl(scraped.companyLinkedInUrl);
  scraped.companyKariyerNetUrl = canonicalizeKariyerNetCompanyUrl(scraped.companyKariyerNetUrl);
  scraped.hrLinkedInUrl = canonicalizeLinkedInProfileUrl(scraped.hrLinkedInUrl);
  // The description is only a plain string out here, so the body scan runs in this scope.
  scraped.hrEmail = scraped.hrEmail ?? extractContactEmail(scraped.description);

  state.scraped = scraped;
  state.scrapeError = scrapeError;

  // Nothing readable and nothing typed yet: say so and offer the blank form, rather than a filled
  // one whose every field is empty. A page with an adapter but no posting id (a board's landing
  // page) lands here too.
  if (!scraped.foundBy && !scraped.title && !scraped.company && !scrapeError) {
    state.screen = "noJob";
    render();
    return;
  }

  state.screen = "form";
  buildForm();
}

// Every call site puts the result inside a double-quoted HTML attribute (buildForm's value="...",
// the autocomplete's data-name="..."), so quotes have to be escaped too. The old textContent →
// innerHTML trick escaped & < > but NOT " — a company name containing a double quote could close
// the attribute early and inject attributes of its own. That is reachable from another user's data:
// Company rows are global (CompanySearchService applies no UserId filter), so names anyone creates
// show up in everyone's autocomplete. MV3's default CSP (script-src 'self') blocks inline handlers,
// which is why this stayed a UI-integrity bug rather than script execution — not a reason to leave
// the escaping incomplete.
function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

main();
