import { getSettings } from "./storage.js";
import { setUpThemeToggle } from "./theme.js";

const content = document.getElementById("content");
const SITE_LABELS = { linkedin: "LinkedIn", kariyer: "kariyer.net" };

// LinkedIn only puts a job on its own /jobs/view/<id> URL when you open it in a dedicated
// tab/page. The far more common path — browsing /jobs/search-results/ (or /jobs/search/,
// /jobs/collections/...) and clicking a result — keeps the URL on that listing page and opens
// the job in a side panel via `?currentJobId=<id>`, never navigating to /jobs/view/ at all.
// Found via manual testing (see DECISIONS.md Sprint 9) — the original /jobs/view/-only pattern
// never matched that far more common case. Both shapes resolve to the same canonical job id, and
// we always submit the canonical https://www.linkedin.com/jobs/view/<id>/ URL to the backend
// (regardless of which LinkedIn page shape the id came from) — LinkedInJobIdExtractor on the
// backend expects that exact shape, and it keeps JobUrl dedup stable across the two entry points.
function extractLinkedInJobId(url) {
  let parsed;
  try {
    parsed = new URL(url);
  } catch {
    return null;
  }

  if (parsed.hostname !== "www.linkedin.com") {
    return null;
  }

  const viewMatch = parsed.pathname.match(/\/jobs\/view\/(\d+)/);
  if (viewMatch) {
    return viewMatch[1];
  }

  const currentJobId = parsed.searchParams.get("currentJobId");
  if (currentJobId && /^\d+$/.test(currentJobId)) {
    return currentJobId;
  }

  return null;
}

// kariyer.net always renders a job at its own /is-ilani/<slug>-<id> URL (no LinkedIn-style side
// panel to account for) and appends the numeric ilan id as the slug's trailing -<digits> segment
// — mirrors KariyerNetJobIdExtractor.cs on the backend, which this must stay in sync with since
// both derive the same Job.ExternalId independently.
function extractKariyerNetJobId(url) {
  let parsed;
  try {
    parsed = new URL(url);
  } catch {
    return null;
  }

  if (parsed.hostname !== "www.kariyer.net") {
    return null;
  }

  const match = parsed.pathname.match(/\/is-ilani\/[^/]*-(\d+)\/?$/);
  return match ? match[1] : null;
}

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

// Picks the current tab's job site (if any) and the canonical URL to submit/dedupe against.
// LinkedIn has a stable id-only canonical form (/jobs/view/<id>/); kariyer.net's slug is part of
// how the posting resolves, so its canonical form is the tab's own path with tracking query
// params/hash stripped, matching that page's own <link rel="canonical"> (verified by manual
// inspection against a live posting).
function detectJob(url) {
  const linkedInJobId = extractLinkedInJobId(url);
  if (linkedInJobId) {
    return { site: "linkedin", jobId: linkedInJobId, jobUrl: `https://www.linkedin.com/jobs/view/${linkedInJobId}/` };
  }

  const kariyerNetJobId = extractKariyerNetJobId(url);
  if (kariyerNetJobId) {
    const parsed = new URL(url);
    return { site: "kariyer", jobId: kariyerNetJobId, jobUrl: `${parsed.origin}${parsed.pathname.replace(/\/$/, "")}` };
  }

  return null;
}

// Injected into the LinkedIn tab via chrome.scripting.executeScript — must be fully
// self-contained (no references to anything outside this function body; jobId is passed in via
// executeScript's `args`, not a closure reference).
//
// LinkedIn's CSS classes here are atomic/hashed (e.g. "_59162b76 d68df9b8 ...") and carry zero
// semantic meaning — confirmed by inspecting two live pages during Sprint 9 manual testing
// (DECISIONS.md), not an assumption; no selector written against those exact hashes would
// survive LinkedIn's next deploy. What's stable instead: the job title and company name are each
// wrapped in an <a> whose href LinkedIn needs for actual routing/SEO — /jobs/view/<id> for the
// title, /company/<slug>/ for the company — verified against both the search-results split-view
// panel and the plain job list card. Scoping the title lookup to the exact jobId (rather than
// "first /jobs/view/ link on the page") avoids picking up an unrelated "similar jobs" link
// elsewhere on the page.
//
// Every field this returns is still shown as an editable input in the popup before submitting —
// scraping failure or a wrong guess degrades to "user fills it in by hand", never a silent wrong
// submission on its own.
async function scrapeLinkedInJob(jobId) {
  function textOf(el) {
    return el?.textContent?.trim() || null;
  }

  const titleLink = document.querySelector(`a[href*="/jobs/view/${jobId}"]`);
  const title = textOf(titleLink);

  const companyLink = document.querySelector('a[href*="/company/"]');
  const company = textOf(companyLink);
  // Raw href, uncanonicalized — this function runs injected into the page (must stay fully
  // self-contained, see the comment block above), so tracking-param stripping happens back in
  // popup.js's own scope once the result comes back (canonicalizeLinkedInCompanyUrl).
  const companyLinkedInUrl = companyLink?.href || null;

  // The location is the first segment (before the "·" separator) of a metadata line like
  // "Istanbul, Türkiye · Reposted 5 days ago · Over 100 people clicked apply", rendered as a
  // <p> sibling of the title's own wrapping element. Found via live-page inspection (Sprint 13,
  // DECISIONS.md): a *fixed* hop count from the title link doesn't work because LinkedIn nests
  // this differently depending on job card variant (promoted vs. not) — 2 levels up from the
  // title's <p> for one, 3 for the other, observed on the same search-results page back to back.
  // Instead: walk up from the title's <p>, and at each ancestor level look for a direct-child
  // <p> that isn't the title's own paragraph and contains "·" — level-count-independent, matches
  // both variants tested. Still the least load-bearing of the three fields: if no page layout
  // matches, the user just types the location in by hand.
  let location = null;
  const titleParagraph = titleLink?.closest("p") ?? null;
  let ancestor = titleParagraph?.parentElement ?? null;
  for (let i = 0; i < 6 && ancestor && !location; i++) {
    const metaParagraph = Array.from(ancestor.children).find(
      (el) => el.tagName === "P" && el !== titleParagraph && el.textContent.includes("·"),
    );
    if (metaParagraph) {
      location = textOf(metaParagraph)?.split("·")[0]?.trim() || null;
    }
    ancestor = ancestor.parentElement;
  }

  // LinkedIn doesn't render the full description text into the DOM up front on this layout —
  // only the truncated, visible portion is there until the "…more" button is clicked, at which
  // point React renders the rest. Click every such button (there's one for "About the job" and
  // a separate one for "About the company") and give React a beat to re-render before reading
  // textContent — found via manual testing (a real ilan came back truncated at "…more").
  document.querySelectorAll('[data-testid="expandable-text-button"]').forEach((button) => button.click());
  await new Promise((resolve) => setTimeout(resolve, 150));

  const descriptionBox = document.querySelector('[data-testid="expandable-text-box"]');
  const description = textOf(descriptionBox);

  // Allow-listed HTML snapshot for a formatted display (bold/headers/bullet lists, same as the
  // original listing) — separate from the plain-text `description` above, which stays untouched
  // for the AI Job Matching prompt. This is a best-effort capture-time filter, NOT a security
  // boundary on its own: the backend stores it as-is and the frontend re-sanitizes with DOMPurify
  // before ever rendering it (see DECISIONS.md — untrusted content is untrusted regardless of
  // which side captured it).
  function sanitizeDescriptionHtml(root) {
    const ALLOWED_TAGS = new Set(["P", "BR", "STRONG", "B", "EM", "I", "UL", "OL", "LI", "H1", "H2", "H3", "H4", "H5", "H6"]);
    const SKIP_TAGS = new Set(["SVG", "BUTTON", "FIGURE", "IMG", "STYLE", "SCRIPT"]);

    function escapeText(text) {
      return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    }

    function walk(node) {
      if (node.nodeType === Node.TEXT_NODE) {
        return escapeText(node.textContent);
      }
      if (node.nodeType !== Node.ELEMENT_NODE) {
        return "";
      }
      const tag = node.tagName;
      if (SKIP_TAGS.has(tag) || node.getAttribute("aria-hidden") === "true") {
        return "";
      }
      const inner = Array.from(node.childNodes).map(walk).join("");
      if (tag === "BR") {
        return "<br>";
      }
      return ALLOWED_TAGS.has(tag) ? `<${tag.toLowerCase()}>${inner}</${tag.toLowerCase()}>` : inner;
    }

    return walk(root).trim();
  }

  const descriptionHtml = descriptionBox ? sanitizeDescriptionHtml(descriptionBox) : null;

  return {
    title: title || "",
    company: company || "",
    location: location || "",
    // Matches CreateFromExtensionRequestValidator's MaximumLength(10_000/20_000) on the backend.
    description: description ? description.slice(0, 10_000) : null,
    descriptionHtml: descriptionHtml ? descriptionHtml.slice(0, 20_000) : null,
    companyLinkedInUrl,
  };
}

// Injected into the kariyer.net tab via chrome.scripting.executeScript — must be fully
// self-contained, same constraint as scrapeLinkedInJob above (hence the duplicated sanitizer
// rather than a shared helper).
//
// Unlike LinkedIn, kariyer.net renders every field directly into the DOM up front (no
// truncated-until-clicked description, confirmed via manual inspection of a live posting), so
// there's no "…more" button to click before reading text. Selectors found via manual inspection
// of https://www.kariyer.net/is-ilani/... (2026-08-28): the title and company are the two
// `.vue-clamp` divs inside the page's single <h1> (title carries an extra `.job-title` class),
// location is `.company-location`'s text, and the full posting body lives in
// `.job-detail-container-description`. As with LinkedIn, every field is still shown as an
// editable input before submitting, so a selector that stops matching after a future kariyer.net
// redesign degrades to "user fills it in by hand," never a wrong silent submission.
async function scrapeKariyerNetJob() {
  function textOf(el) {
    return el?.textContent?.trim() || null;
  }

  const title = textOf(document.querySelector("h1 div.vue-clamp.job-title"));
  const company = textOf(document.querySelector("h1 div.vue-clamp:not(.job-title)"));
  const location = textOf(document.querySelector(".company-location"));

  // Same allow-listed HTML snapshot as LinkedIn's scraper — see sanitizeDescriptionHtml above for
  // the untrusted-content rationale (this is a capture-time best effort only; the backend and
  // frontend both re-sanitize before ever rendering it).
  function sanitizeDescriptionHtml(root) {
    const ALLOWED_TAGS = new Set(["P", "BR", "STRONG", "B", "EM", "I", "UL", "OL", "LI", "H1", "H2", "H3", "H4", "H5", "H6"]);
    const SKIP_TAGS = new Set(["SVG", "BUTTON", "FIGURE", "IMG", "STYLE", "SCRIPT"]);

    function escapeText(text) {
      return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    }

    function walk(node) {
      if (node.nodeType === Node.TEXT_NODE) {
        return escapeText(node.textContent);
      }
      if (node.nodeType !== Node.ELEMENT_NODE) {
        return "";
      }
      const tag = node.tagName;
      if (SKIP_TAGS.has(tag) || node.getAttribute("aria-hidden") === "true") {
        return "";
      }
      const inner = Array.from(node.childNodes).map(walk).join("");
      if (tag === "BR") {
        return "<br>";
      }
      return ALLOWED_TAGS.has(tag) ? `<${tag.toLowerCase()}>${inner}</${tag.toLowerCase()}>` : inner;
    }

    return walk(root).trim();
  }

  const descriptionBox = document.querySelector(".job-detail-container-description");
  const description = textOf(descriptionBox);
  const descriptionHtml = descriptionBox ? sanitizeDescriptionHtml(descriptionBox) : null;

  return {
    title: title || "",
    company: company || "",
    location: location || "",
    // Matches CreateFromExtensionRequestValidator's MaximumLength(10_000/20_000) on the backend.
    description: description ? description.slice(0, 10_000) : null,
    descriptionHtml: descriptionHtml ? descriptionHtml.slice(0, 20_000) : null,
    // kariyer.net postings never link to a LinkedIn company page.
    companyLinkedInUrl: null,
  };
}

function render(html) {
  content.innerHTML = html;
}

function renderMessage(text, linkText, onLinkClick) {
  render(`<p class="muted">${text}</p>`);
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

async function main() {
  setUpThemeToggle("themeToggle");
  document.getElementById("openOptionsBtn")?.addEventListener("click", () => chrome.runtime.openOptionsPage());
  const settings = await getSettings();

  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  const job = tab?.url ? detectJob(tab.url) : null;
  if (!job) {
    renderMessage(
      "Open a LinkedIn job posting (a /jobs/view/ page, or a job selected in search results) or a kariyer.net job posting (an /is-ilani/ page) to track it here.",
    );
    return;
  }
  const jobUrl = job.jobUrl;

  if (!settings.token) {
    renderMessage("Set up your e-kariyerim access token first.", "Open Settings", () => chrome.runtime.openOptionsPage());
    return;
  }

  let scraped;
  let scrapeError = null;
  try {
    const [{ result }] = await chrome.scripting.executeScript({
      target: { tabId: tab.id },
      func: job.site === "linkedin" ? scrapeLinkedInJob : scrapeKariyerNetJob,
      args: job.site === "linkedin" ? [job.jobId] : [],
    });
    scraped = result;
  } catch (error) {
    scraped = { title: "", company: "", location: "", description: null, descriptionHtml: null, companyLinkedInUrl: null };
    // Surfaced inline (not just console.error) so a manual tester doesn't need DevTools open to
    // see why fields came back empty — found necessary in Sprint 9 manual testing, where the
    // silently-swallowed error made an actual scrape failure look identical to "nothing found".
    scrapeError = error?.message || String(error);
  }

  // The scraper (injected via executeScript, self-contained) returns a raw href — tracking
  // params/fragment stripping happens here, back in the extension's own scope.
  scraped.companyLinkedInUrl = canonicalizeLinkedInCompanyUrl(scraped.companyLinkedInUrl);

  render(`
    <span class="site-badge">${escapeHtml(SITE_LABELS[job.site])}</span>
    ${scrapeError ? `<p class="status error">Auto-fill failed: ${escapeHtml(scrapeError)}</p>` : ""}
    <label for="companyName">Company</label>
    <div class="combobox">
      <input id="companyName" type="text" autocomplete="off" value="${escapeHtml(scraped.company)}" />
      <ul id="companySuggestions" class="suggestions" hidden></ul>
    </div>

    <label for="jobTitle">Job title</label>
    <input id="jobTitle" type="text" value="${escapeHtml(scraped.title)}" />

    <label for="location">Location</label>
    <input id="location" type="text" value="${escapeHtml(scraped.location)}" />

    <button id="submit">I Applied</button>
    <p id="status" class="status" hidden></p>
  `);

  setUpCompanyAutocomplete(settings);

  document.getElementById("submit").addEventListener("click", async () => {
    const submitButton = document.getElementById("submit");
    const statusEl = document.getElementById("status");
    submitButton.disabled = true;
    statusEl.hidden = true;

    const companyName = document.getElementById("companyName").value.trim();
    const jobTitle = document.getElementById("jobTitle").value.trim();
    const location = document.getElementById("location").value.trim();

    if (!companyName || !jobTitle) {
      statusEl.textContent = "Company and job title are required.";
      statusEl.className = "status error";
      statusEl.hidden = false;
      submitButton.disabled = false;
      return;
    }

    try {
      const response = await fetch(`${settings.apiBaseUrl}/api/applications/from-extension`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${settings.token}`,
        },
        body: JSON.stringify({
          companyName,
          jobTitle,
          jobUrl,
          location: location || null,
          description: scraped.description,
          descriptionHtml: scraped.descriptionHtml,
          publishedAt: null,
          companyLinkedInUrl: scraped.companyLinkedInUrl,
        }),
      });

      if (!response.ok) {
        throw new Error(`Request failed (${response.status})`);
      }

      const result = await response.json();
      statusEl.textContent = result.wasDuplicate
        ? "Already tracked — opened your existing application."
        : "Added to e-kariyerim.";
      statusEl.className = "status success";
      statusEl.hidden = false;
    } catch {
      statusEl.textContent = "Could not reach e-kariyerim. Check your Settings (API base URL/token).";
      statusEl.className = "status error";
      statusEl.hidden = false;
      submitButton.disabled = false;
    }
  });
}

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value;
  return div.innerHTML;
}

main();
